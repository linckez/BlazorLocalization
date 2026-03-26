using BlazorLocalization.Extensions;
using BlazorLocalization.Extensions.Providers.JsonFile;
using MudBlazor.Services;
using MudBlazorServerSample.Components;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddMudServices();

// Replaces services.AddLocalization() — registers IStringLocalizerFactory backed by pluggable providers.
builder.Services.AddProviderBasedLocalization(builder.Configuration)
    .AddJsonFileTranslationProvider();

// MVC controller support for CultureController (cookie-based culture switching).
builder.Services.AddControllers();

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);

app.UseHttpsRedirection();

app.UseAntiforgery();

// Culture detection — still part of ASP.NET Core's request pipeline.
var supportedCultures = new[] { "en-US", "de", "pl", "da" };
app.UseRequestLocalization(new RequestLocalizationOptions()
    .SetDefaultCulture(supportedCultures[0])
    .AddSupportedCultures(supportedCultures)
    .AddSupportedUICultures(supportedCultures));

app.MapControllers();
app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
