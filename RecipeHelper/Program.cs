using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.SqlServer;
using Microsoft.Extensions.DependencyInjection;
using OpenAI;
using OpenTelemetry;
using OpenTelemetry.Exporter;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using RecipeHelper;
using RecipeHelper.Controllers;
using RecipeHelper.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();
builder.Services.AddHttpContextAccessor();
builder.Services.AddHttpClient();
builder.Services.AddMemoryCache();
// Session (used below) requires IDistributedCache specifically -- AddMemoryCache()
// above registers IMemoryCache, a different interface, so without this Session
// throws as soon as anything touches HttpContext.Session.
builder.Services.AddDistributedMemoryCache();
builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo("/var/lib/recipehelper/keys"))
    .SetApplicationName("RecipeHelper");
builder.Services.AddScoped<KrogerService, KrogerService>();  // Registering your Kroger service
// Singleton (not Scoped): StorageService builds a ClientSecretCredential + BlobServiceClient
// in its constructor. All fields are set once there and never mutated, and the Azure SDK
// clients are documented thread-safe/immutable, so this is safe to share across requests --
// and lets Azure.Identity's in-memory AAD token cache actually persist between requests
// instead of being rebuilt (and its cache thrown away) on every single upload.
builder.Services.AddSingleton<StorageService, StorageService>();
builder.Services.AddScoped<SpoonacularService, SpoonacularService>();
builder.Services.AddScoped<RecipeService, RecipeService>();
builder.Services.AddScoped<ProductService, ProductService>();
builder.Services.AddScoped<KrogerAuthService, KrogerAuthService>();
builder.Services.AddScoped<ImportService, ImportService>();
builder.Services.AddScoped<MeasurementService, MeasurementService>();
builder.Services.AddScoped<IngredientsService, IngredientsService>();
builder.Services.AddScoped<ShoppingListService, ShoppingListService>();
builder.Services.AddScoped<MealPlanService, MealPlanService>();
builder.Services.AddScoped<AccountService, AccountService>();

// App login. The cookie is encrypted with the data protection keys persisted above,
// so it survives restarts and deploys -- signing in is meant to be a once-per-device
// thing, like a native app. It lasts a year and slides: any visit in the second half
// of that year re-issues it for another full year.
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.Cookie.Name = "RecipeHelper.Auth";
        options.Cookie.HttpOnly = true;
        options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
        // Lax (not Strict) so the cookie is still sent when Kroger's OAuth login
        // redirects back to /auth/callback -- a cross-site top-level navigation.
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.ExpireTimeSpan = TimeSpan.FromDays(365);
        options.SlidingExpiration = true;
        options.LoginPath = "/Account/Login";
        options.LogoutPath = "/Account/Logout";
        options.AccessDeniedPath = "/Account/Login";
        options.Events.OnRedirectToLogin = context =>
        {
            // A signed-out fetch() gets a plain 401 instead of a redirect to the login
            // page's HTML, which it can't use. Page scripts already reload on a failed
            // request, and that reload is a navigation, which does get the redirect.
            var fetchMode = context.Request.Headers["Sec-Fetch-Mode"].ToString();
            if (!string.IsNullOrEmpty(fetchMode) && fetchMode != "navigate")
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            else
                context.Response.Redirect(context.RedirectUri);
            return Task.CompletedTask;
        };
    });

// Every endpoint requires a signed-in user unless it opts out with [AllowAnonymous]
// (login/setup/join, public share links, the error page). Static files are served
// before routing, so CSS/JS/icons/manifest/sw.js stay public.
builder.Services.AddAuthorization(options =>
{
    options.FallbackPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();
});

// Brute-force guard on the login/setup/join POSTs: 10 attempts per 5 minutes per
// client IP (the real IP from nginx's X-Forwarded-For, see UseForwardedHeaders below).
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy(AccountController.RateLimitPolicy, httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 10,
                Window = TimeSpan.FromMinutes(5),
                QueueLimit = 0,
            }));
    options.OnRejected = async (context, token) =>
    {
        context.HttpContext.Response.ContentType = "text/plain";
        await context.HttpContext.Response.WriteAsync("Too many sign-in attempts. Wait a few minutes and try again.", token);
    };
});

// nginx terminates TLS and proxies to Kestrel over plain HTTP on localhost. Trust its
// X-Forwarded-For/Proto (loopback proxies are trusted by default) so the app sees the
// real client IP (rate limiting) and the real https scheme (Secure cookies, links).
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
});

builder.Services.AddSingleton(new OpenAIClient(
    apiKey: builder.Configuration["OpenAI:ApiKey"]
));

builder.Services.AddDbContext<DatabaseContext>(options =>
    options.UseSqlServer(builder.Configuration["ConnectionString"] ?? throw new InvalidOperationException("Connection string 'ConnectionString' not found.")));
// Add session services
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30); // Set session timeout
    options.Cookie.HttpOnly = true; // Make the session cookie HttpOnly
    options.Cookie.IsEssential = true; // Make the session cookie essential
});
builder.Services.AddControllers();

// OpenTelemetry: read from appsettings first, fall back to OTEL_* env vars.
var otelServiceName = builder.Configuration["OpenTelemetry:ServiceName"]
    ?? Environment.GetEnvironmentVariable("OTEL_SERVICE_NAME")
    ?? "recipe-helper";
var otelServiceNamespace = builder.Configuration["OpenTelemetry:ServiceNamespace"]
    ?? "recipe-helper";
var otlpEndpoint = builder.Configuration["OpenTelemetry:Otlp:Endpoint"]
    ?? Environment.GetEnvironmentVariable("OTEL_EXPORTER_OTLP_ENDPOINT");
var otlpHeaders = builder.Configuration["OpenTelemetry:Otlp:Headers"]
    ?? Environment.GetEnvironmentVariable("OTEL_EXPORTER_OTLP_HEADERS");
var otlpProtocol = builder.Configuration["OpenTelemetry:Otlp:Protocol"]
    ?? Environment.GetEnvironmentVariable("OTEL_EXPORTER_OTLP_PROTOCOL")
    ?? "http/protobuf";

// NOTE: When OtlpExporterOptions.Endpoint is set programmatically, the SDK does NOT append
// the per-signal path (/v1/traces, /v1/metrics, /v1/logs). It only appends them when the
// value comes from the OTEL_EXPORTER_OTLP_ENDPOINT env var. So we append the path ourselves
// per signal — otherwise Grafana Cloud's OTLP gateway gets POSTs to bare /otlp and silently drops them.
void ConfigureOtlp(OtlpExporterOptions opts, string signalPath)
{
    if (!string.IsNullOrWhiteSpace(otlpEndpoint))
    {
        var baseUri = otlpEndpoint.TrimEnd('/');
        opts.Endpoint = new Uri($"{baseUri}/{signalPath}");
    }
    if (!string.IsNullOrWhiteSpace(otlpHeaders))
        opts.Headers = otlpHeaders;
    opts.Protocol = otlpProtocol.Equals("grpc", StringComparison.OrdinalIgnoreCase)
        ? OtlpExportProtocol.Grpc
        : OtlpExportProtocol.HttpProtobuf;
}

var otelResource = ResourceBuilder.CreateDefault()
    .AddService(serviceName: otelServiceName, serviceNamespace: otelServiceNamespace, serviceVersion: "1.0.0")
    .AddAttributes(new KeyValuePair<string, object>[]
    {
        new("deployment.environment", builder.Environment.EnvironmentName),
        new("host.name", Environment.MachineName)
    });

builder.Services.AddOpenTelemetry()
    .ConfigureResource(r => r.AddService(serviceName: otelServiceName, serviceNamespace: otelServiceNamespace, serviceVersion: "1.0.0"))
    .WithTracing(t => t
        .AddSource("RecipeHelper.*")
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddSqlClientInstrumentation()
        .AddOtlpExporter(o => ConfigureOtlp(o, "v1/traces")))
    .WithMetrics(m => m
        .AddMeter("RecipeHelper.*")
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddRuntimeInstrumentation()
        .AddOtlpExporter(o => ConfigureOtlp(o, "v1/metrics")));

builder.Logging.AddOpenTelemetry(o =>
{
    o.SetResourceBuilder(otelResource);
    o.IncludeFormattedMessage = true;
    o.IncludeScopes = true;
    o.AddOtlpExporter(e => ConfigureOtlp(e, "v1/logs"));
});

var app = builder.Build();

app.UseForwardedHeaders();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles(new StaticFileOptions
{
    OnPrepareResponse = ctx =>
    {
        var headers = ctx.Context.Response.Headers;
        if (ctx.File.Name == "sw.js")
        {
            headers["Cache-Control"] = "no-store";
        }
        else if (ctx.File.Name.EndsWith(".css") || ctx.File.Name.EndsWith(".js"))
        {
            // asp-append-version fingerprints these URLs; safe to cache long-term
            headers["Cache-Control"] = "public, max-age=31536000, immutable";
        }
        else
        {
            headers["Cache-Control"] = "public, max-age=86400";
        }
    }
});

app.UseRouting();

app.UseRateLimiter();

app.UseAuthentication();
app.UseAuthorization();

app.UseSession();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Recipe}/{action=Recipe}/{id?}");

app.Run();
