using System.Runtime.CompilerServices;

namespace BlazorLocalization.Extractor.Tests;

static class ModuleInit
{
	[ModuleInitializer]
	public static void Init()
	{
		// Prevent Verify's DiffEngine from launching diff tools (e.g. Rider) on mismatches.
		// In CI or automated runs, we want test failure output, not GUI windows.
		DiffEngine.DiffRunner.Disabled = true;
	}
}