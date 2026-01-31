using System.Globalization;
using System.Text;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.IdentityModel.Tokens;
using ModelFarm.Application;
using ModelFarm.Application.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorPages();

// JWT Settings
var jwtSettings = new JwtSettings
{
    Secret = builder.Configuration["Jwt:Secret"] ?? "ModelFarmDefaultSecretKey12345678901234567890",
    Issuer = builder.Configuration["Jwt:Issuer"] ?? "ModelFarm",
    Audience = builder.Configuration["Jwt:Audience"] ?? "ModelFarm",
    AccessTokenExpirationMinutes = 60,
    RefreshTokenExpirationDays = 7
};
builder.Services.AddSingleton(jwtSettings);

// Add cookie authentication for Razor Pages
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Account/Login";
        options.LogoutPath = "/Account/Logout";
        options.AccessDeniedPath = "/Account/AccessDenied";
        options.ExpireTimeSpan = TimeSpan.FromDays(7);
        options.SlidingExpiration = true;
    });

// Require authentication by default for all pages
builder.Services.AddAuthorization(options =>
{
    options.FallbackPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();
});

// Add HttpContextAccessor for user context service
builder.Services.AddHttpContextAccessor();

// Add ModelFarm application services (includes Infrastructure)
builder.Services.AddApplication();

var app = builder.Build();

// Use invariant culture for consistent number parsing (decimal point, not comma)
var cultureInfo = CultureInfo.InvariantCulture;
CultureInfo.DefaultThreadCurrentCulture = cultureInfo;
CultureInfo.DefaultThreadCurrentUICulture = cultureInfo;

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();

// Static files must be served before authentication to allow CSS/JS to load on login page
app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapStaticAssets()
   .AllowAnonymous();
app.MapRazorPages()
   .WithStaticAssets();

app.Run();
