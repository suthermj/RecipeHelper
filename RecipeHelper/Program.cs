using Azure.Identity;
using Azure.Monitor.OpenTelemetry.Exporter;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.SqlServer;
using Microsoft.Extensions.DependencyInjection;
using OpenAI;
using OpenTelemetry;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using RecipeHelper;
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

// OpenTelemetry → Azure Application Insights. Ingestion authenticates with the same Entra
// service principal as blob storage (needs "Monitoring Metrics Publisher" on the App Insights
// resource), so the connection string only says *where* to send — it isn't a secret once
// local auth is disabled on the resource. No connection string (e.g. local dev) = no export.
var otelServiceName = builder.Configuration["OpenTelemetry:ServiceName"] ?? "recipe-helper";
var appInsightsConnectionString = builder.Configuration["ApplicationInsights:ConnectionString"];
var exportToAzureMonitor = !string.IsNullOrWhiteSpace(appInsightsConnectionString);
// One credential shared by all three exporters so they share its token cache.
var azureMonitorCredential = exportToAzureMonitor
    ? new ClientSecretCredential(
        builder.Configuration["AzureAd:TenantId"],
        builder.Configuration["AzureAd:ClientId"],
        builder.Configuration["AzureAd:ClientSecret"])
    : null;

void ConfigureAzureMonitor(AzureMonitorExporterOptions opts)
{
    opts.ConnectionString = appInsightsConnectionString;
    opts.Credential = azureMonitorCredential;
}

var otelResource = ResourceBuilder.CreateDefault()
    .AddService(serviceName: otelServiceName, serviceVersion: "1.0.0")
    .AddAttributes(new KeyValuePair<string, object>[]
    {
        new("deployment.environment", builder.Environment.EnvironmentName),
        new("host.name", Environment.MachineName)
    });

builder.Services.AddOpenTelemetry()
    .ConfigureResource(r => r.AddService(serviceName: otelServiceName, serviceVersion: "1.0.0"))
    .WithTracing(t =>
    {
        t.AddSource("RecipeHelper.*")
            .AddAspNetCoreInstrumentation(o =>
            {
                // Static assets (sw.js, manifest.json, css/js, images) were ~70% of AppRequests
                // and drown out the real requests. MVC routes have no file extension, so they're
                // all kept; a missing static file still shows in nginx's access log.
                o.Filter = ctx => !Path.HasExtension(ctx.Request.Path.Value);
                // The conventional route makes every request's name the template
                // ("GET {controller=Recipe}/{action=Recipe}/{id?}"), so AppRequests can't be
                // grouped by endpoint. Rename to the action that actually ran -- the Azure
                // Monitor exporter builds the request name from http.route.
                o.EnrichWithHttpResponse = (activity, response) =>
                {
                    // url.scheme is captured before UseForwardedHeaders runs, so it'd always
                    // say http (nginx -> localhost); re-read it once the scheme is corrected.
                    activity.SetTag("url.scheme", response.HttpContext.Request.Scheme);
                    var values = response.HttpContext.GetRouteData()?.Values;
                    if (values?["controller"] is string controller && values["action"] is string action)
                    {
                        var route = $"{controller}/{action}";
                        activity.SetTag("http.route", route);
                        activity.DisplayName = $"{response.HttpContext.Request.Method} {route}";
                    }
                };
            })
            .AddHttpClientInstrumentation()
            .AddSqlClientInstrumentation();
        if (exportToAzureMonitor)
            t.AddAzureMonitorTraceExporter(ConfigureAzureMonitor);
    })
    .WithMetrics(m =>
    {
        m.AddMeter("RecipeHelper.*")
            .AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation()
            .AddRuntimeInstrumentation();
        if (exportToAzureMonitor)
            m.AddAzureMonitorMetricExporter(ConfigureAzureMonitor);
    });

builder.Logging.AddOpenTelemetry(o =>
{
    o.SetResourceBuilder(otelResource);
    o.IncludeFormattedMessage = true;
    o.IncludeScopes = true;
    if (exportToAzureMonitor)
        o.AddAzureMonitorLogExporter(ConfigureAzureMonitor);
});
// Framework Information logs ("Request starting", "Sending file", "Executed DbCommand", ...)
// duplicate what AppRequests/AppDependencies already record, so only Warning+ from them
// goes to App Insights. Set in code because appsettings.json is gitignored and never
// reaches prod. Console (journalctl) is unaffected and keeps the full detail.
builder.Logging.AddFilter<OpenTelemetryLoggerProvider>("Microsoft", LogLevel.Warning);
builder.Logging.AddFilter<OpenTelemetryLoggerProvider>("System", LogLevel.Warning);
builder.Logging.AddFilter<OpenTelemetryLoggerProvider>("Microsoft.Hosting.Lifetime", LogLevel.Information);

// nginx terminates TLS and proxies to localhost:5000 with X-Forwarded-For/-Proto. Without
// this the app sees every request as http from 127.0.0.1 (wrong URLs in telemetry, and
// HttpsRedirection warns it can't determine the https port). The default trusted proxy
// list is loopback only, which is exactly where nginx sits.
builder.Services.Configure<ForwardedHeadersOptions>(o =>
    o.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto);

var app = builder.Build();

// Configure the HTTP request pipeline.
app.UseForwardedHeaders();
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

app.UseAuthorization();

app.UseSession();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Recipe}/{action=Recipe}/{id?}");

app.Run();
