using BlazorLocalization.Extensions;
using BlazorLocalization.TranslationProvider.Crowdin;
using MudBlazor.Services;
using NeoSmart.Caching.Sqlite;
using SampleBlazorApp.Components;
using SampleBlazorApp.Services;

var builder = WebApplication.CreateBuilder(args);

// Add MudBlazor services
builder.Services.AddMudServices();

// Localization:
builder.Services.AddSqliteCache(o => o.CachePath = "translations.db");

builder.Services.AddProviderBasedLocalization(builder.Configuration);

// Stub providers — uncomment one pair at a time to test different architectures.
// DictionaryTranslationProvider: per-key lookup, simulates a plain SQL database.
// JsonFanoutTranslationProvider: sentinel+fan-out with JSON, simulates a Crowdin-like CDN.
// builder.Services.AddSingleton<ITranslationProvider, DictionaryTranslationProvider>();
// builder.Services.AddSingleton<ITranslationProvider, JsonFanoutTranslationProvider>();

// Real Crowdin OTA provider — uncomment (and comment stubs above) to use a live distribution.
// builder.Services.AddProviderBasedLocalization(builder.Configuration)
//     .AddCrowdinTranslationProvider();

// Add MVC controller support for CultureController
builder.Services.AddControllers();

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);

app.UseHttpsRedirection();

app.UseAntiforgery();

var supportedCultures = new[] { "en-US", "da", "es-MX" };
var localizationOptions = new RequestLocalizationOptions()
    .SetDefaultCulture(supportedCultures[0])
    .AddSupportedCultures(supportedCultures)
    .AddSupportedUICultures(supportedCultures);

app.UseRequestLocalization(localizationOptions);

app.MapControllers();
app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
