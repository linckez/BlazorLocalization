using BlazorLocalization.Extractor.Domain;
using BlazorLocalization.Extractor.Domain.Calls;
using BlazorLocalization.Extractor.Domain.Entries;
using BlazorLocalization.Extractor.Domain.Calls;
using BlazorLocalization.Extractor.Domain.Entries;
using BlazorLocalization.Extractor.Scanning.Sources;
using Microsoft.CodeAnalysis;

namespace BlazorLocalization.Extractor.Scanning.Extractors;

/// <summary>
/// A chain link that preserves the <see cref="IMethodSymbol"/> for identity-based dispatch.
/// </summary>
internal sealed record ResolvedChainLink(
	string MethodName,
	IReadOnlyList<ResolvedArgument> Arguments,
	IMethodSymbol? Symbol);

/// <summary>
/// Interprets fluent builder chains into <see cref="TranslationEntry"/> records
/// using symbol identity from <see cref="BuilderSymbolTable"/>. Zero string-based dispatch.
/// </summary>
/// <remarks>
/// All methods are static — the <see cref="BuilderSymbolTable"/> is passed as a parameter.
/// </remarks>
internal static class ChainInterpreter
{
	/// <summary>
	/// Interprets a <c>Translate()</c> call and its fluent chain into a <see cref="TranslationEntry"/>
	/// using symbol identity from <see cref="BuilderSymbolTable"/>. Zero string-based dispatch.
	/// </summary>
	public static TranslationEntry? InterpretTranslateCall(
		ExtractedCall call,
		IMethodSymbol translateMethodSymbol,
		IReadOnlyList<ResolvedChainLink>? chain,
		BuilderSymbolTable symbols)
	{
		var source = MakeSource(call);

		var key = FindArgByParam(call.Arguments, symbols.TranslateKeyParam);
		if (key is null)
			return null;

		var builderKind = symbols.ClassifyReturnType(translateMethodSymbol.ReturnType);

		if (chain is not { Count: > 0 })
		{
			if (builderKind is BuilderKind.Simple)
			{
				var message = FindArgByParam(call.Arguments, symbols.TranslateMessageParam);
				var sourceText = message is not null ? new SingularText(message) : null;
				return new TranslationEntry(key, sourceText, source);
			}

			return new TranslationEntry(key, null, source);
		}

		return builderKind switch
		{
			BuilderKind.Simple => InterpretSimpleChain(call, chain, key, source, symbols),
			BuilderKind.Plural => InterpretPluralChain(call, chain, key, source, symbols),
			BuilderKind.Select => InterpretSelectChain(chain, key, source, symbols),
			BuilderKind.SelectPlural => InterpretSelectPluralChain(call, chain, key, source, symbols),
			_ => null
		};
	}

	/// <summary>
	/// Interprets a <c>TranslationDefinitions.DefineSimple/DefinePlural/DefineSelect/DefineSelectPlural()</c>
	/// definition factory call and its fluent chain into a <see cref="TranslationEntry"/>.
	/// </summary>
	public static TranslationEntry? InterpretDefinitionCall(
		ExtractedCall call,
		IMethodSymbol factoryMethodSymbol,
		IReadOnlyList<ResolvedChainLink>? chain,
		BuilderSymbolTable symbols)
	{
		var source = MakeSource(call);

		var key = FindArgByParam(call.Arguments, symbols.DefKeyParam);
		if (key is null)
			return null;

		var builderKind = symbols.ClassifyReturnType(factoryMethodSymbol.ReturnType);

		if (chain is not { Count: > 0 })
		{
			if (builderKind is BuilderKind.Simple)
			{
				var message = FindArgByParam(call.Arguments, symbols.DefSimpleMessageParam);
				var sourceText = message is not null ? new SingularText(message) : null;
				return new TranslationEntry(key, sourceText, source);
			}

			return new TranslationEntry(key, null, source);
		}

		return builderKind switch
		{
			BuilderKind.Simple => InterpretSimpleChain(call, chain, key, source, symbols),
			BuilderKind.Plural => InterpretPluralChain(call, chain, key, source, symbols),
			BuilderKind.Select => InterpretSelectChain(chain, key, source, symbols),
			BuilderKind.SelectPlural => InterpretSelectPluralChain(call, chain, key, source, symbols),
			_ => null
		};
	}

	private static TranslationEntry InterpretSimpleChain(
		ExtractedCall call,
		IReadOnlyList<ResolvedChainLink> chain,
		string key,
		SourceReference source,
		BuilderSymbolTable symbols)
	{
		var message = FindArgByParam(call.Arguments, symbols.TranslateMessageParam);
		var sourceText = message is not null ? new SingularText(message) : null;

		Dictionary<string, TranslationSourceText>? inlineTranslations = null;

		foreach (var link in chain)
		{
			if (link.Symbol is null) continue;

			if (symbols.IsSimpleFor(link.Symbol))
			{
				var locale = FindArgByParam(link.Arguments, symbols.SimpleForLocaleParam);
				var msg = FindLiteralByParam(link.Arguments, symbols.SimpleForMessageParam);
				if (locale is not null && msg is not null)
				{
					inlineTranslations ??= new();
					inlineTranslations[locale] = new SingularText(msg);
				}
			}
		}

		return new TranslationEntry(key, sourceText, source, inlineTranslations);
	}

	private static TranslationEntry InterpretPluralChain(
		ExtractedCall call,
		IReadOnlyList<ResolvedChainLink> chain,
		string key,
		SourceReference source,
		BuilderSymbolTable symbols)
	{
		var categories = new Dictionary<string, string>();
		var exactMatches = new Dictionary<int, string>();
		var isOrdinal = string.Equals(FindArgByParam(call.Arguments, symbols.TranslateOrdinalParam), "true", StringComparison.OrdinalIgnoreCase);
		var forSections = new List<(string locale, List<ResolvedChainLink> calls)>();
		string? currentLocale = null;
		List<ResolvedChainLink>? currentForCalls = null;

		foreach (var link in chain)
		{
			if (link.Symbol is null) continue;

			if (symbols.IsPluralFor(link.Symbol))
			{
				if (currentLocale is not null && currentForCalls is not null)
					forSections.Add((currentLocale, currentForCalls));
				currentLocale = FindArgByParam(link.Arguments, symbols.PluralForLocaleParam);
				currentForCalls = [];
				continue;
			}

			if (currentLocale is not null)
			{
				currentForCalls?.Add(link);
				continue;
			}

			if (symbols.IsPluralExactly(link.Symbol))
			{
				var valueStr = FindArgByParam(link.Arguments, symbols.ExactlyValueParam);
				var msg = FindLiteralByParam(link.Arguments, symbols.ExactlyMessageParam);
				if (valueStr is not null && int.TryParse(valueStr, out var exactValue) && msg is not null)
					exactMatches[exactValue] = msg;
				continue;
			}

			if (symbols.AllPluralCategoryMethods.Contains(link.Symbol.OriginalDefinition))
			{
				var msg = FindLiteralByParam(link.Arguments, symbols.CategoryMessageParam);
				if (msg is not null)
					categories[link.MethodName] = msg;
			}
		}

		if (currentLocale is not null && currentForCalls is not null)
			forSections.Add((currentLocale, currentForCalls));

		var sourceText = categories.ContainsKey(nameof(BlazorLocalization.Extensions.Translation.PluralBuilder.Other))
			? BuildPluralText(categories, exactMatches, isOrdinal)
			: null;

		var inlineTranslations = forSections.Count > 0
			? BuildPluralInlineTranslations(forSections, isOrdinal, symbols)
			: null;

		return new TranslationEntry(key, sourceText, source, inlineTranslations);
	}

	private static TranslationEntry InterpretSelectChain(
		IReadOnlyList<ResolvedChainLink> chain,
		string key,
		SourceReference source,
		BuilderSymbolTable symbols)
	{
		var cases = new Dictionary<string, string>();
		string? otherwise = null;
		var forSections = new List<(string locale, List<ResolvedChainLink> calls)>();
		string? currentLocale = null;
		List<ResolvedChainLink>? currentForCalls = null;

		foreach (var link in chain)
		{
			if (link.Symbol is null) continue;

			if (symbols.IsSelectFor(link.Symbol))
			{
				if (currentLocale is not null && currentForCalls is not null)
					forSections.Add((currentLocale, currentForCalls));
				currentLocale = FindArgByParam(link.Arguments, symbols.SelectForLocaleParam);
				currentForCalls = [];
				continue;
			}

			if (currentLocale is not null)
			{
				currentForCalls?.Add(link);
				continue;
			}

			if (symbols.IsSelectWhen(link.Symbol))
			{
				var selectValue = StripEnumPrefix(FindArgByParam(link.Arguments, symbols.SelectWhenSelectParam));
				var msg = FindLiteralByParam(link.Arguments, symbols.SelectWhenMessageParam);
				if (selectValue is not null && msg is not null)
					cases[selectValue] = msg;
			}
			else if (symbols.IsSelectOtherwise(link.Symbol))
			{
				otherwise = FindLiteralByParam(link.Arguments, symbols.SelectOtherwiseMessageParam);
			}
		}

		if (currentLocale is not null && currentForCalls is not null)
			forSections.Add((currentLocale, currentForCalls));

		var sourceText = cases.Count > 0 || otherwise is not null
			? new SelectText(cases, otherwise)
			: null;

		var inlineTranslations = forSections.Count > 0
			? BuildSelectInlineTranslations(forSections, symbols)
			: null;

		return new TranslationEntry(key, sourceText, source, inlineTranslations);
	}

	private static TranslationEntry InterpretSelectPluralChain(
		ExtractedCall call,
		IReadOnlyList<ResolvedChainLink> chain,
		string key,
		SourceReference source,
		BuilderSymbolTable symbols)
	{
		var cases = new Dictionary<string, PluralText>();
		var isOrdinal = string.Equals(FindArgByParam(call.Arguments, symbols.TranslateOrdinalParam), "true", StringComparison.OrdinalIgnoreCase);
		var forSections = new List<(string locale, List<ResolvedChainLink> calls)>();
		string? currentLocale = null;
		List<ResolvedChainLink>? currentForCalls = null;

		string? currentSelectCase = null;
		var currentCategories = new Dictionary<string, string>();
		var currentExact = new Dictionary<int, string>();
		var selectCaseStarted = false;
		PluralText? otherwisePlural = null;

		void FlushSelectCase()
		{
			if (!selectCaseStarted || (currentCategories.Count == 0 && currentExact.Count == 0)) return;
			var plural = BuildPluralText(currentCategories, currentExact, isOrdinal);
			if (currentSelectCase is not null)
				cases[currentSelectCase] = plural;
		}

		foreach (var link in chain)
		{
			if (link.Symbol is null) continue;

			if (symbols.IsSelectPluralFor(link.Symbol))
			{
				FlushSelectCase();
				if (selectCaseStarted && currentSelectCase is null && (currentCategories.Count > 0 || currentExact.Count > 0))
					otherwisePlural = BuildPluralText(currentCategories, currentExact, isOrdinal);
				selectCaseStarted = false;
				if (currentLocale is not null && currentForCalls is not null)
					forSections.Add((currentLocale, currentForCalls));
				currentLocale = FindArgByParam(link.Arguments, symbols.SelectPluralForLocaleParam);
				currentForCalls = [];
				continue;
			}

			if (currentLocale is not null)
			{
				currentForCalls?.Add(link);
				continue;
			}

			if (symbols.IsSelectPluralWhen(link.Symbol))
			{
				FlushSelectCase();
				currentSelectCase = StripEnumPrefix(FindArgByParam(link.Arguments, symbols.SelectPluralWhenSelectParam));
				currentCategories = new Dictionary<string, string>();
				currentExact = new Dictionary<int, string>();
				selectCaseStarted = true;
				continue;
			}

			if (symbols.IsSelectPluralOtherwise(link.Symbol) && link.Arguments.Count == 0)
			{
				FlushSelectCase();
				currentSelectCase = null;
				currentCategories = new Dictionary<string, string>();
				currentExact = new Dictionary<int, string>();
				selectCaseStarted = true;
				continue;
			}

			if (symbols.IsSelectPluralExactly(link.Symbol))
			{
				selectCaseStarted = true;
				var valueStr = FindArgByParam(link.Arguments, symbols.ExactlyValueParam);
				var msg = FindLiteralByParam(link.Arguments, symbols.ExactlyMessageParam);
				if (valueStr is not null && int.TryParse(valueStr, out var exactValue) && msg is not null)
					currentExact[exactValue] = msg;
				continue;
			}

			if (symbols.AllSelectPluralCategoryMethods.Contains(link.Symbol.OriginalDefinition))
			{
				selectCaseStarted = true;
				var msg = FindLiteralByParam(link.Arguments, symbols.CategoryMessageParam);
				if (msg is not null)
					currentCategories[link.MethodName] = msg;
			}
		}

		FlushSelectCase();
		if (selectCaseStarted && currentSelectCase is null && (currentCategories.Count > 0 || currentExact.Count > 0))
			otherwisePlural = BuildPluralText(currentCategories, currentExact, isOrdinal);
		if (currentLocale is not null && currentForCalls is not null)
			forSections.Add((currentLocale, currentForCalls));

		TranslationSourceText? sourceText = null;
		if (cases.Count > 0 || otherwisePlural is not null)
			sourceText = new SelectPluralText(cases, otherwisePlural);

		var inlineTranslations = forSections.Count > 0
			? BuildSelectPluralInlineTranslations(forSections, isOrdinal, symbols)
			: null;

		return new TranslationEntry(key, sourceText, source, inlineTranslations);
	}

	private static PluralText BuildPluralText(
		Dictionary<string, string> categories,
		Dictionary<int, string> exactMatches,
		bool isOrdinal)
	{
		return new PluralText(
			Other: categories.GetValueOrDefault(nameof(BlazorLocalization.Extensions.Translation.PluralBuilder.Other), ""),
			Zero: categories.GetValueOrDefault(nameof(BlazorLocalization.Extensions.Translation.PluralBuilder.Zero)),
			One: categories.GetValueOrDefault(nameof(BlazorLocalization.Extensions.Translation.PluralBuilder.One)),
			Two: categories.GetValueOrDefault(nameof(BlazorLocalization.Extensions.Translation.PluralBuilder.Two)),
			Few: categories.GetValueOrDefault(nameof(BlazorLocalization.Extensions.Translation.PluralBuilder.Few)),
			Many: categories.GetValueOrDefault(nameof(BlazorLocalization.Extensions.Translation.PluralBuilder.Many)),
			ExactMatches: exactMatches.Count > 0 ? exactMatches : null,
			IsOrdinal: isOrdinal);
	}

	private static IReadOnlyDictionary<string, TranslationSourceText>? BuildPluralInlineTranslations(
		List<(string locale, List<ResolvedChainLink> calls)> forSections,
		bool isOrdinal,
		BuilderSymbolTable symbols)
	{
		var result = new Dictionary<string, TranslationSourceText>();

		foreach (var (locale, calls) in forSections)
		{
			var categories = new Dictionary<string, string>();
			var exactMatches = new Dictionary<int, string>();

			foreach (var link in calls)
			{
				if (link.Symbol is null) continue;

				if (symbols.IsPluralExactly(link.Symbol))
				{
					var valueStr = FindArgByParam(link.Arguments, symbols.ExactlyValueParam);
					var msg = FindLiteralByParam(link.Arguments, symbols.ExactlyMessageParam);
					if (valueStr is not null && int.TryParse(valueStr, out var exactValue) && msg is not null)
						exactMatches[exactValue] = msg;
				}
				else if (symbols.AllPluralCategoryMethods.Contains(link.Symbol.OriginalDefinition))
				{
					var msg = FindLiteralByParam(link.Arguments, symbols.CategoryMessageParam);
					if (msg is not null)
						categories[link.MethodName] = msg;
				}
			}

			if (categories.Count > 0 || exactMatches.Count > 0)
				result[locale] = BuildPluralText(categories, exactMatches, isOrdinal);
		}

		return result.Count > 0 ? result : null;
	}

	private static IReadOnlyDictionary<string, TranslationSourceText>? BuildSelectInlineTranslations(
		List<(string locale, List<ResolvedChainLink> calls)> forSections,
		BuilderSymbolTable symbols)
	{
		var result = new Dictionary<string, TranslationSourceText>();

		foreach (var (locale, calls) in forSections)
		{
			var cases = new Dictionary<string, string>();
			string? otherwise = null;

			foreach (var link in calls)
			{
				if (link.Symbol is null) continue;

				if (symbols.IsSelectWhen(link.Symbol))
				{
					var selectValue = StripEnumPrefix(FindArgByParam(link.Arguments, symbols.SelectWhenSelectParam));
					var msg = FindLiteralByParam(link.Arguments, symbols.SelectWhenMessageParam);
					if (selectValue is not null && msg is not null)
						cases[selectValue] = msg;
				}
				else if (symbols.IsSelectOtherwise(link.Symbol))
				{
					otherwise = FindLiteralByParam(link.Arguments, symbols.SelectOtherwiseMessageParam);
				}
			}

			if (cases.Count > 0 || otherwise is not null)
				result[locale] = new SelectText(cases, otherwise);
		}

		return result.Count > 0 ? result : null;
	}

	private static IReadOnlyDictionary<string, TranslationSourceText>? BuildSelectPluralInlineTranslations(
		List<(string locale, List<ResolvedChainLink> calls)> forSections,
		bool isOrdinal,
		BuilderSymbolTable symbols)
	{
		var result = new Dictionary<string, TranslationSourceText>();

		foreach (var (locale, calls) in forSections)
		{
			var cases = new Dictionary<string, PluralText>();
			PluralText? otherwisePlural = null;
			string? currentSelectCase = null;
			var currentCategories = new Dictionary<string, string>();
			var currentExact = new Dictionary<int, string>();
			var selectCaseStarted = false;

			void Flush()
			{
				if (!selectCaseStarted || (currentCategories.Count == 0 && currentExact.Count == 0)) return;
				var plural = BuildPluralText(currentCategories, currentExact, isOrdinal);
				if (currentSelectCase is not null)
					cases[currentSelectCase] = plural;
			}

			foreach (var link in calls)
			{
				if (link.Symbol is null) continue;

				if (symbols.IsSelectPluralWhen(link.Symbol))
				{
					Flush();
					currentSelectCase = StripEnumPrefix(FindArgByParam(link.Arguments, symbols.SelectPluralWhenSelectParam));
					currentCategories = new Dictionary<string, string>();
					currentExact = new Dictionary<int, string>();
					selectCaseStarted = true;
				}
				else if (symbols.IsSelectPluralOtherwise(link.Symbol) && link.Arguments.Count == 0)
				{
					Flush();
					currentSelectCase = null;
					currentCategories = new Dictionary<string, string>();
					currentExact = new Dictionary<int, string>();
					selectCaseStarted = true;
				}
				else if (symbols.IsSelectPluralExactly(link.Symbol))
				{
					selectCaseStarted = true;
					var valueStr = FindArgByParam(link.Arguments, symbols.ExactlyValueParam);
					var msg = FindLiteralByParam(link.Arguments, symbols.ExactlyMessageParam);
					if (valueStr is not null && int.TryParse(valueStr, out var exactValue) && msg is not null)
						currentExact[exactValue] = msg;
				}
				else if (symbols.AllSelectPluralCategoryMethods.Contains(link.Symbol.OriginalDefinition))
				{
					selectCaseStarted = true;
					var msg = FindLiteralByParam(link.Arguments, symbols.CategoryMessageParam);
					if (msg is not null)
						currentCategories[link.MethodName] = msg;
				}
			}

			Flush();
			if (selectCaseStarted && currentSelectCase is null && (currentCategories.Count > 0 || currentExact.Count > 0))
				otherwisePlural = BuildPluralText(currentCategories, currentExact, isOrdinal);

			if (cases.Count > 0 || otherwisePlural is not null)
				result[locale] = new SelectPluralText(cases, otherwisePlural);
		}

		return result.Count > 0 ? result : null;
	}

	internal static string? FindArgByParam(IReadOnlyList<ResolvedArgument> args, string paramName) =>
		args.FirstOrDefault(a => a.ParameterName == paramName)?.Value;

	private static string? FindLiteralByParam(IReadOnlyList<ResolvedArgument> args, string paramName) =>
		args.FirstOrDefault(a => a.ParameterName == paramName && a.IsLiteral)?.Value;

	internal static SourceReference MakeSource(ExtractedCall call) =>
		new(call.Location.FilePath, call.Location.Line, call.Location.ProjectName,
			$"{call.ContainingTypeName}.{call.MethodName}");

	private static string? StripEnumPrefix(string? value)
	{
		if (value is null) return null;
		var lastDot = value.LastIndexOf('.');
		return lastDot >= 0 ? value[(lastDot + 1)..] : value;
	}
}
