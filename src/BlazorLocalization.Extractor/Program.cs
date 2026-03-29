using System.Reflection;
using BlazorLocalization.Extractor.Cli;
using BlazorLocalization.Extractor.Cli.Commands;
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
		.WithDescription("Scan Blazor projects for IStringLocalizer usage, extract source strings, and export to a translation file (i18next JSON, PO, or generic JSON).")
		.WithExample("extract", "./src", "-f", "i18next", "-o", "./translations")
		.WithExample("extract", "./src", "--format", "po", "--output", "./locale");

	config.AddCommand<InspectCommand>("inspect")
		.WithDescription("Show all detected IStringLocalizer calls and the resulting translation entries. Useful for debugging what the scanner sees before exporting.")
		.WithExample("inspect", "./src")
		.WithExample("inspect", "./src", "./lib/Shared");
});

if (args.Length == 0)
	args = InteractiveWizard.Run();

return app.Run(args);