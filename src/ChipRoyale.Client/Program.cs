using System.Globalization;
using ChipRoyale.Client;
using ChipRoyale.Client.Localization;
using ChipRoyale.Client.Services;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.JSInterop;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddScoped(_ => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });
builder.Services.AddScoped<BrowserStorage>();
builder.Services.AddScoped<GameServer>();
builder.Services.AddScoped<PlayerService>();

var host = builder.Build();

// Language: saved choice, otherwise the browser language (German is the default).
var js = host.Services.GetRequiredService<IJSRuntime>();
var saved = await js.InvokeAsync<string?>("chipRoyale.getCulture");
var language = saved ?? (CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "en" ? "en" : Loc.DefaultCulture);
var culture = CultureInfo.GetCultureInfo(Loc.SupportedCultures.Contains(language) ? language : Loc.DefaultCulture);
CultureInfo.DefaultThreadCurrentCulture = culture;
CultureInfo.DefaultThreadCurrentUICulture = culture;
CultureInfo.CurrentCulture = culture;
CultureInfo.CurrentUICulture = culture;

await host.RunAsync();
