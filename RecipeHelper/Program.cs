using Microsoft.AspNetCore.DataProtection;
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
using RecipeHelper.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();
builder.Services.AddHttpContextAccessor();
builder.Services.AddHttpClient();
builder.Services.AddMemoryCache();
builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo("/var/lib/recipehelper/keys"))
    .SetApplicationName("RecipeHelper");
builder.Services.AddScoped<KrogerService, KrogerService>();  // Registering your Kroger service
builder.Services.AddScoped<StorageService, StorageService>();
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
        .AddSqlClientInstrumentation(o => o.SetDbStatementForText = true)
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

app.UseAuthorization();

app.UseSession();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Recipe}/{action=Recipe}/{id?}");

app.Run();
