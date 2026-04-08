using System.Reflection;
using BlazorLocalization.Extractor.Adapters.Cli;
using BlazorLocalization.Extractor.Adapters.Cli.Commands;
using Spectre.Console.Cli;

var version = typeof(Program).Assembly
    .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
    ?.InformationalVersion.Split('+')[0] ?? "unknown";

var app = new CommandApp();

app.Configure(config =>
{
    config.SetApplicationName("blazor-loc");
    config.SetApplicationVersion(version);

    config.AddCommand<ExtractCommand>("extract")
        .WithDescription("Scan your projects for translation strings and export to translation files (i18next JSON, PO, or generic JSON).")
        .WithExample("extract", "./src", "-f", "i18next", "-o", "./translations")
        .WithExample("extract", "./src", "--format", "po", "--output", "./locale");

    config.AddCommand<InspectCommand>("inspect")
        .WithDescription("Check your translation setup: every key with its status, locale coverage, and potential issues.")
        .WithExample("inspect", "./src")
        .WithExample("inspect", "./src", "./lib/Shared");
});

if (args.Length == 0)
    args = InteractiveWizard.Run();

return app.Run(args);
