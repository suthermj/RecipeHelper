using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore.SqlServer;
using RecipeHelper;
using RecipeHelper.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();
builder.Services.AddHttpContextAccessor();
builder.Services.AddHttpClient();
builder.Services.AddScoped<KrogerService, KrogerService>();  // Registering your Kroger service
builder.Services.AddScoped<StorageService, StorageService>();
builder.Services.AddScoped<SpoonacularService, SpoonacularService>();
builder.Services.AddScoped<RecipeService, RecipeService>();
builder.Services.AddScoped<ProductService, ProductService>();
builder.Services.AddScoped<KrogerAuthService, KrogerAuthService>();
builder.Services.AddScoped<ImportService, ImportService>();

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
var app = builder.Build();



// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthorization();

app.UseSession();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Recipe}/{action=Recipe}/{id?}");

app.Run();
