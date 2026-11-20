using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using ChipRoyale.Web.Components;
using ChipRoyale.Web.Components.Account;
using ChipRoyale.Web.Data;
using ChipRoyale.Web.Localization;
using ChipRoyale.Web.Services;
using Microsoft.AspNetCore.Localization;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddCascadingAuthenticationState();
builder.Services.AddScoped<IdentityRedirectManager>();
builder.Services.AddScoped<AuthenticationStateProvider, IdentityRevalidatingAuthenticationStateProvider>();

builder.Services.AddAuthentication(options =>
    {
        options.DefaultScheme = IdentityConstants.ApplicationScheme;
        options.DefaultSignInScheme = IdentityConstants.ExternalScheme;
    })
    .AddIdentityCookies();

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
connectionString = PlayerDataLocation.Resolve(connectionString, builder.Configuration["Storage:DataDirectory"], builder.Environment.ContentRootPath);
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlite(connectionString));
// Interactive components need short-lived contexts (one per operation).
builder.Services.AddDbContextFactory<ApplicationDbContext>(options =>
    options.UseSqlite(connectionString), ServiceLifetime.Scoped);
builder.Services.AddScoped<PlayerService>();
builder.Services.AddLocalization();
builder.Services.AddDatabaseDeveloperPageExceptionFilter();

builder.Services.AddIdentityCore<ApplicationUser>(options =>
    {
        // Off until a real email sender exists (see SPIELKONZEPT V3); otherwise nobody could confirm an account.
        options.SignIn.RequireConfirmedAccount = builder.Configuration.GetValue("Auth:RequireConfirmedAccount", false);
        options.Stores.SchemaVersion = IdentitySchemaVersions.Version3;
        // A game account: 6+ characters, no special-character rules.
        options.Password.RequiredLength = 6;
        options.Password.RequireDigit = false;
        options.Password.RequireLowercase = false;
        options.Password.RequireUppercase = false;
        options.Password.RequireNonAlphanumeric = false;
        options.Password.RequiredUniqueChars = 1;
    })
    .AddErrorDescriber<LocalizedIdentityErrorDescriber>()
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddSignInManager()
    .AddDefaultTokenProviders();

builder.Services.AddSingleton<IEmailSender<ApplicationUser>, IdentityNoOpEmailSender>();

var app = builder.Build();

// Create / update the SQLite database on start.
using (var scope = app.Services.CreateScope())
{
    scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Database.Migrate();
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseMigrationsEndPoint();
}
else
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

// Languages: German (default) and English, chosen by cookie or browser language.
app.UseRequestLocalization(new RequestLocalizationOptions()
    .SetDefaultCulture(Loc.DefaultCulture)
    .AddSupportedCultures(Loc.SupportedCultures)
    .AddSupportedUICultures(Loc.SupportedCultures));

app.UseAntiforgery();

app.MapStaticAssets();

// Switch language: stores the culture cookie and goes back to the page.
app.MapGet("/Culture/Set", (string culture, string? redirectUri, HttpContext context) =>
{
    if (Loc.SupportedCultures.Contains(culture))
    {
        context.Response.Cookies.Append(
            CookieRequestCultureProvider.DefaultCookieName,
            CookieRequestCultureProvider.MakeCookieValue(new RequestCulture(culture)),
            new CookieOptions { MaxAge = TimeSpan.FromDays(365), IsEssential = true, SameSite = SameSiteMode.Lax });
    }
    return Results.LocalRedirect(string.IsNullOrEmpty(redirectUri) || !redirectUri.StartsWith('/') ? "/" : redirectUri);
});
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

// Add additional endpoints required by the Identity /Account Razor components.
app.MapAdditionalIdentityEndpoints();

app.Run();
