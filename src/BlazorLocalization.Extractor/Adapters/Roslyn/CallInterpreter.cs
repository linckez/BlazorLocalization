using BlazorLocalization.Extractor.Domain;
using Microsoft.CodeAnalysis;

namespace BlazorLocalization.Extractor.Adapters.Roslyn;

/// <summary>
/// Converts <see cref="ScannedCallSite"/> (with its fluent chain) into domain types:
/// <see cref="TranslationDefinition"/> or <see cref="TranslationReference"/>.
///
/// Dispatches by the method's **return type** against pre-resolved builder/definition types.
/// Builder return → inline translation (or key-only reference if no source text in chain).
/// Definition return → factory call (DefineXxx).
/// String/other → reference (indexer, GetString, definition-replay).
/// </summary>
internal static class CallInterpreter
{
    private static readonly HashSet<string> PluralCategoryNames =
        ExtensionsContract.PluralCategoryNames;

    /// <summary>
    /// Classify a scanned call site and produce the appropriate domain type.
    /// Routes by return type when <paramref name="resolvedTypes"/> is available.
    /// </summary>
    public static (TranslationDefinition? Definition, TranslationReference? Reference) Interpret(
        ScannedCallSite call, SourceFilePath file, ResolvedTypes? resolvedTypes)
    {
        if (call.ResolvedMethod is not null && resolvedTypes is not null)
        {
            var returnType = call.ResolvedMethod.ReturnType;

            // Builder return type → inline Translation() call (with or without source text)
            if (resolvedTypes.IsBuilder(returnType, out var matchedBuilder))
                return InterpretBuilderCall(call, file, matchedBuilder, resolvedTypes);

            // Definition return type → DefineXxx factory
            if (resolvedTypes.IsDefinitionType(returnType))
                return InterpretDefinitionFactory(call, file);

            // String return → definition-replay (Translation(SimpleDefinition, ...))
            // or Display() — these are reference-only
        }

        // Fallback: indexer, GetString, definition-replay, Display, or unresolved
        return InterpretAsReference(call, file);
    }

    /// <summary>
    /// Handles Translation() calls that return a builder type (SimpleBuilder, PluralBuilder, etc.).
    /// Extracts the key, delegates to the form-specific interpreter, and always emits a reference.
    /// If the interpreter produces a definition (source text found), emits that too.
    /// </summary>
    private static (TranslationDefinition?, TranslationReference?) InterpretBuilderCall(
        ScannedCallSite call, SourceFilePath file, INamedTypeSymbol matchedBuilder, ResolvedTypes types)
    {
        var keyArg = FindArg(call.Arguments, "key");
        if (keyArg is null || !keyArg.Value.TryGetString(out var key) || key is null)
            return (null, null);

        var defSite = new DefinitionSite(file, call.Line, DefinitionKind.InlineTranslation, FormatContext(call));

        // Route by which builder type the method returns
        (TranslationDefinition?, TranslationReference?) result;
        if (SymbolEqualityComparer.Default.Equals(matchedBuilder, types.SimpleBuilder))
        {
            var messageArg = FindArg(call.Arguments, "message");
            result = messageArg is not null
                ? InterpretSimple(key, messageArg, call.Chain, defSite)
                : (null, null);
        }
        else if (SymbolEqualityComparer.Default.Equals(matchedBuilder, types.PluralBuilder))
        {
            var ordinalArg = FindArg(call.Arguments, "ordinal");
            var isOrdinal = ordinalArg?.Value is OperationValue.Constant { Value: true };
            result = InterpretPlural(key, isOrdinal, call.Chain, defSite);
        }
        else if (SymbolEqualityComparer.Default.Equals(matchedBuilder, types.SelectBuilder))
        {
            result = InterpretSelect(key, call.Chain, defSite);
        }
        else if (SymbolEqualityComparer.Default.Equals(matchedBuilder, types.SelectPluralBuilder))
        {
            var ordinalArg = FindArg(call.Arguments, "ordinal");
            var isOrdinal = ordinalArg?.Value is OperationValue.Constant { Value: true };
            result = InterpretSelectPlural(key, isOrdinal, call.Chain, defSite);
        }
        else
        {
            result = (null, null);
        }

        // Always emit a reference (this call site uses the key)
        var refSite = new ReferenceSite(file, call.Line);
        var reference = new TranslationReference(key, true, refSite);

        // If the interpreter produced a definition (source text found), return both
        return (result.Item1, reference);
    }

    /// <summary>
    /// Handles DefineXxx() factory calls. These produce definitions only, not references.
    /// </summary>
    private static (TranslationDefinition?, TranslationReference?) InterpretDefinitionFactory(
        ScannedCallSite call, SourceFilePath file)
    {
        var keyArg = FindArg(call.Arguments, "key");
        if (keyArg is null || !keyArg.Value.TryGetString(out var key) || key is null)
            return (null, null);

        var defSite = new DefinitionSite(file, call.Line, DefinitionKind.ReusableDefinition, FormatContext(call));
        return InterpretDefinitionFactoryByName(call, key, call.Chain, defSite);
    }

    private static (TranslationDefinition?, TranslationReference?) InterpretAsReference(
        ScannedCallSite call, SourceFilePath file)
    {
        // Try common parameter names for the key, then fall back to position 0
        var keyArg = FindArg(call.Arguments, "key")
                     ?? FindArg(call.Arguments, "name")
                     ?? FindArgByPosition(call.Arguments, 0);

        if (keyArg is null) return (null, null);

        string? key = null;
        if (keyArg.Value.TryGetString(out var k))
            key = k;
        else if (keyArg.Value is OperationValue.Constant { Value: var v })
            key = v?.ToString();

        if (key is null)
        {
            // Translation(definition) and Display(enum) calls use pre-defined keys —
            // the key comes from DefineXxx/[Translation] definitions, not this call site.
            // Only indexer/GetString calls with non-literal keys should produce dynamic refs.
            if (call.MethodName is ExtensionsContract.Translation or ExtensionsContract.Display)
                return (null, null);

            // Non-literal key — still a reference, just not resolvable
            var syntax = keyArg.Value switch
            {
                OperationValue.SymbolReference { Symbol: var sym } => sym.Name,
                OperationValue.Unrecognized { Syntax: var s } => s,
                _ => null
            };

            if (syntax is not null)
            {
                var site = new ReferenceSite(file, call.Line);
                return (null, new TranslationReference(syntax, false, site));
            }

            return (null, null);
        }

        var isLiteral = keyArg.Value.IsLiteral;
        var context = call.CallKind == CallKind.Indexer ? "IStringLocalizer.this[]" : null;
        var refSite = new ReferenceSite(file, call.Line, context);
        return (null, new TranslationReference(key, isLiteral, refSite));
    }

    // ─── Simple ──────────────────────────────────────────────────────

    private static (TranslationDefinition?, TranslationReference?) InterpretSimple(
        string key,
        ScannedArgument messageArg,
        IReadOnlyList<FluentChainWalker.ChainLink>? chain,
        DefinitionSite site)
    {
        messageArg.Value.TryGetString(out var message);
        var sourceText = message is not null
            ? new SingularText(message)
            : (TranslationSourceText?)null;

        if (sourceText is null)
            return (null, null);

        var inlines = CollectSimpleInlineTranslations(chain);

        return (new TranslationDefinition(key, sourceText, site, inlines), null);
    }

    private static IReadOnlyDictionary<string, TranslationSourceText>? CollectSimpleInlineTranslations(
        IReadOnlyList<FluentChainWalker.ChainLink>? chain)
    {
        if (chain is null or { Count: 0 }) return null;

        Dictionary<string, TranslationSourceText>? result = null;

        foreach (var link in chain)
        {
            if (link.MethodName != "For") continue;

            var locale = FindChainArgString(link, "locale", 0);
            var msg = FindChainArgString(link, "message", 1);

            if (locale is not null && msg is not null)
            {
                result ??= new(StringComparer.OrdinalIgnoreCase);
                result.TryAdd(locale, new SingularText(msg));
            }
        }

        return result;
    }

    // ─── Plural ──────────────────────────────────────────────────────

    private static (TranslationDefinition?, TranslationReference?) InterpretPlural(
        string key,
        bool isOrdinal,
        IReadOnlyList<FluentChainWalker.ChainLink>? chain,
        DefinitionSite site)
    {
        var categories = new Dictionary<string, string>();
        var exactMatches = new Dictionary<int, string>();
        var forSections = new List<(string locale, List<FluentChainWalker.ChainLink> links)>();
        string? currentLocale = null;
        List<FluentChainWalker.ChainLink>? currentForLinks = null;

        if (chain is not null)
        {
            foreach (var link in chain)
            {
                if (link.MethodName == "For")
                {
                    if (currentLocale is not null && currentForLinks is not null)
                        forSections.Add((currentLocale, currentForLinks));
                    currentLocale = FindChainArgString(link, "locale", 0);
                    currentForLinks = [];
                    continue;
                }

                if (currentLocale is not null) { currentForLinks?.Add(link); continue; }

                if (link.MethodName == "Exactly")
                {
                    var valStr = FindChainArgString(link, "value", 0);
                    var msg = FindChainArgString(link, "message", 1);
                    if (valStr is not null && int.TryParse(valStr, out var exact) && msg is not null)
                        exactMatches[exact] = msg;
                    continue;
                }

                if (PluralCategoryNames.Contains(link.MethodName))
                {
                    var msg = FindChainArgString(link, "message", 0);
                    if (msg is not null) categories[link.MethodName] = msg;
                }
            }

            if (currentLocale is not null && currentForLinks is not null)
                forSections.Add((currentLocale, currentForLinks));
        }

        var sourceText = categories.ContainsKey("Other")
            ? BuildPluralText(categories, exactMatches, isOrdinal)
            : null;

        if (sourceText is null) return (null, null);

        var inlines = BuildPluralInlineTranslations(forSections, isOrdinal);
        return (new TranslationDefinition(key, sourceText, site, inlines), null);
    }

    // ─── Select ──────────────────────────────────────────────────────

    private static (TranslationDefinition?, TranslationReference?) InterpretSelect(
        string key,
        IReadOnlyList<FluentChainWalker.ChainLink>? chain,
        DefinitionSite site)
    {
        var cases = new Dictionary<string, string>();
        string? otherwise = null;
        var forSections = new List<(string locale, List<FluentChainWalker.ChainLink> links)>();
        string? currentLocale = null;
        List<FluentChainWalker.ChainLink>? currentForLinks = null;

        if (chain is not null)
        {
            foreach (var link in chain)
            {
                if (link.MethodName == "For")
                {
                    if (currentLocale is not null && currentForLinks is not null)
                        forSections.Add((currentLocale, currentForLinks));
                    currentLocale = FindChainArgString(link, "locale", 0);
                    currentForLinks = [];
                    continue;
                }

                if (currentLocale is not null) { currentForLinks?.Add(link); continue; }

                if (link.MethodName == "When")
                {
                    var selectValue = FindChainArgString(link, "select", 0);
                    var msg = FindChainArgString(link, "message", 1);
                    if (selectValue is not null && msg is not null)
                        cases[StripEnumPrefix(selectValue)] = msg;
                }
                else if (link.MethodName == "Otherwise")
                {
                    otherwise = FindChainArgString(link, "message", 0);
                }
            }

            if (currentLocale is not null && currentForLinks is not null)
                forSections.Add((currentLocale, currentForLinks));
        }

        if (cases.Count == 0 && otherwise is null) return (null, null);

        var sourceText = new SelectText(cases, otherwise);
        var inlines = BuildSelectInlineTranslations(forSections);
        return (new TranslationDefinition(key, sourceText, site, inlines), null);
    }

    // ─── SelectPlural ────────────────────────────────────────────────

    private static (TranslationDefinition?, TranslationReference?) InterpretSelectPlural(
        string key,
        bool isOrdinal,
        IReadOnlyList<FluentChainWalker.ChainLink>? chain,
        DefinitionSite site)
    {
        var cases = new Dictionary<string, PluralText>();
        PluralText? otherwisePlural = null;
        var forSections = new List<(string locale, List<FluentChainWalker.ChainLink> links)>();
        string? currentLocale = null;
        List<FluentChainWalker.ChainLink>? currentForLinks = null;

        // State for accumulating plural categories within a select case
        string? currentSelectCase = null;
        var currentCategories = new Dictionary<string, string>();
        var currentExact = new Dictionary<int, string>();
        var selectCaseStarted = false;

        void FlushSelectCase()
        {
            if (!selectCaseStarted || (currentCategories.Count == 0 && currentExact.Count == 0)) return;
            var plural = BuildPluralText(currentCategories, currentExact, isOrdinal);
            if (currentSelectCase is not null)
                cases[currentSelectCase] = plural;
            else
                otherwisePlural = plural;
        }

        if (chain is not null)
        {
            foreach (var link in chain)
            {
                // Entering a .For() locale section
                if (link.MethodName == "For")
                {
                    FlushSelectCase();
                    selectCaseStarted = false;
                    currentSelectCase = null;
                    currentCategories = [];
                    currentExact = [];

                    if (currentLocale is not null && currentForLinks is not null)
                        forSections.Add((currentLocale, currentForLinks));
                    currentLocale = FindChainArgString(link, "locale", 0);
                    currentForLinks = [];
                    continue;
                }

                // Inside a .For() section — collect links for later interpretation
                if (currentLocale is not null) { currentForLinks?.Add(link); continue; }

                // .When(select) — new select case
                if (link.MethodName == "When")
                {
                    FlushSelectCase();
                    currentSelectCase = StripEnumPrefix(FindChainArgString(link, "select", 0) ?? "");
                    currentCategories = [];
                    currentExact = [];
                    selectCaseStarted = true;
                    continue;
                }

                // .Otherwise() — fallback select case (no arg)
                if (link.MethodName == "Otherwise" && link.Arguments.Count == 0)
                {
                    FlushSelectCase();
                    currentSelectCase = null;
                    currentCategories = [];
                    currentExact = [];
                    selectCaseStarted = true;
                    continue;
                }

                // .Exactly(value, message) — exact match within current case
                if (link.MethodName == "Exactly")
                {
                    selectCaseStarted = true;
                    var valStr = FindChainArgString(link, "value", 0);
                    var msg = FindChainArgString(link, "message", 1);
                    if (valStr is not null && int.TryParse(valStr, out var exact) && msg is not null)
                        currentExact[exact] = msg;
                    continue;
                }

                // Plural category (.One, .Other, etc.)
                if (PluralCategoryNames.Contains(link.MethodName))
                {
                    selectCaseStarted = true;
                    var msg = FindChainArgString(link, "message", 0);
                    if (msg is not null) currentCategories[link.MethodName] = msg;
                }
            }

            FlushSelectCase();
            if (currentLocale is not null && currentForLinks is not null)
                forSections.Add((currentLocale, currentForLinks));
        }

        if (cases.Count == 0 && otherwisePlural is null) return (null, null);

        var sourceText = new SelectPluralText(cases, otherwisePlural);
        var inlines = BuildSelectPluralInlineTranslations(forSections, isOrdinal);
        return (new TranslationDefinition(key, sourceText, site, inlines), null);
    }

    private static IReadOnlyDictionary<string, TranslationSourceText>? BuildSelectPluralInlineTranslations(
        List<(string locale, List<FluentChainWalker.ChainLink> links)> forSections,
        bool isOrdinal)
    {
        if (forSections.Count == 0) return null;
        var result = new Dictionary<string, TranslationSourceText>(StringComparer.OrdinalIgnoreCase);

        foreach (var (locale, links) in forSections)
        {
            var cases = new Dictionary<string, PluralText>();
            PluralText? otherwisePlural = null;

            string? currentSelectCase = null;
            var currentCategories = new Dictionary<string, string>();
            var currentExact = new Dictionary<int, string>();
            var selectCaseStarted = false;

            void FlushCase()
            {
                if (!selectCaseStarted || (currentCategories.Count == 0 && currentExact.Count == 0)) return;
                var plural = BuildPluralText(currentCategories, currentExact, isOrdinal);
                if (currentSelectCase is not null)
                    cases[currentSelectCase] = plural;
                else
                    otherwisePlural = plural;
            }

            foreach (var link in links)
            {
                if (link.MethodName == "When")
                {
                    FlushCase();
                    currentSelectCase = StripEnumPrefix(FindChainArgString(link, "select", 0) ?? "");
                    currentCategories = [];
                    currentExact = [];
                    selectCaseStarted = true;
                }
                else if (link.MethodName == "Otherwise" && link.Arguments.Count == 0)
                {
                    FlushCase();
                    currentSelectCase = null;
                    currentCategories = [];
                    currentExact = [];
                    selectCaseStarted = true;
                }
                else if (link.MethodName == "Exactly")
                {
                    selectCaseStarted = true;
                    var valStr = FindChainArgString(link, "value", 0);
                    var msg = FindChainArgString(link, "message", 1);
                    if (valStr is not null && int.TryParse(valStr, out var exact) && msg is not null)
                        currentExact[exact] = msg;
                }
                else if (PluralCategoryNames.Contains(link.MethodName))
                {
                    selectCaseStarted = true;
                    var msg = FindChainArgString(link, "message", 0);
                    if (msg is not null) currentCategories[link.MethodName] = msg;
                }
            }

            FlushCase();
            if (cases.Count > 0 || otherwisePlural is not null)
                result[locale] = new SelectPluralText(cases, otherwisePlural);
        }

        return result.Count > 0 ? result : null;
    }

    // ─── Definition Factories ────────────────────────────────────────

    private static (TranslationDefinition?, TranslationReference?) InterpretDefinitionFactoryByName(
        ScannedCallSite call,
        string key,
        IReadOnlyList<FluentChainWalker.ChainLink>? chain,
        DefinitionSite site)
    {
        if (call.MethodName == ExtensionsContract.DefineSimple)
        {
            var messageArg = FindArg(call.Arguments, "message");
            if (messageArg is null) return (null, null);
            messageArg.Value.TryGetString(out var msg);
            if (msg is null) return (null, null);

            var inlines = CollectSimpleInlineTranslations(chain);
            return (new TranslationDefinition(key, new SingularText(msg), site, inlines), null);
        }

        // DefinePlural, DefineSelect, DefineSelectPlural — key only, text comes from chain
        if (call.MethodName == ExtensionsContract.DefinePlural)
            return InterpretPlural(key, false, chain, site);

        if (call.MethodName == ExtensionsContract.DefineSelect)
            return InterpretSelect(key, chain, site);

        if (call.MethodName == ExtensionsContract.DefineSelectPlural)
            return InterpretSelectPlural(key, false, chain, site);

        return (null, null);
    }

    // ─── Shared helpers ──────────────────────────────────────────────

    private static PluralText BuildPluralText(
        Dictionary<string, string> categories,
        Dictionary<int, string> exactMatches,
        bool isOrdinal)
    {
        return new PluralText(
            Other: categories.GetValueOrDefault("Other", ""),
            Zero: categories.GetValueOrDefault("Zero"),
            One: categories.GetValueOrDefault("One"),
            Two: categories.GetValueOrDefault("Two"),
            Few: categories.GetValueOrDefault("Few"),
            Many: categories.GetValueOrDefault("Many"),
            ExactMatches: exactMatches.Count > 0 ? exactMatches : null,
            IsOrdinal: isOrdinal);
    }

    private static IReadOnlyDictionary<string, TranslationSourceText>? BuildPluralInlineTranslations(
        List<(string locale, List<FluentChainWalker.ChainLink> links)> forSections,
        bool isOrdinal)
    {
        if (forSections.Count == 0) return null;
        var result = new Dictionary<string, TranslationSourceText>(StringComparer.OrdinalIgnoreCase);

        foreach (var (locale, links) in forSections)
        {
            var categories = new Dictionary<string, string>();
            var exactMatches = new Dictionary<int, string>();

            foreach (var link in links)
            {
                if (link.MethodName == "Exactly")
                {
                    var valStr = FindChainArgString(link, "value", 0);
                    var msg = FindChainArgString(link, "message", 1);
                    if (valStr is not null && int.TryParse(valStr, out var exact) && msg is not null)
                        exactMatches[exact] = msg;
                }
                else if (PluralCategoryNames.Contains(link.MethodName))
                {
                    var msg = FindChainArgString(link, "message", 0);
                    if (msg is not null) categories[link.MethodName] = msg;
                }
            }

            if (categories.Count > 0 || exactMatches.Count > 0)
                result[locale] = BuildPluralText(categories, exactMatches, isOrdinal);
        }

        return result.Count > 0 ? result : null;
    }

    private static IReadOnlyDictionary<string, TranslationSourceText>? BuildSelectInlineTranslations(
        List<(string locale, List<FluentChainWalker.ChainLink> links)> forSections)
    {
        if (forSections.Count == 0) return null;
        var result = new Dictionary<string, TranslationSourceText>(StringComparer.OrdinalIgnoreCase);

        foreach (var (locale, links) in forSections)
        {
            var cases = new Dictionary<string, string>();
            string? otherwise = null;

            foreach (var link in links)
            {
                if (link.MethodName == "When")
                {
                    var selectValue = FindChainArgString(link, "select", 0);
                    var msg = FindChainArgString(link, "message", 1);
                    if (selectValue is not null && msg is not null)
                        cases[StripEnumPrefix(selectValue)] = msg;
                }
                else if (link.MethodName == "Otherwise")
                {
                    otherwise = FindChainArgString(link, "message", 0);
                }
            }

            if (cases.Count > 0 || otherwise is not null)
                result[locale] = new SelectText(cases, otherwise);
        }

        return result.Count > 0 ? result : null;
    }

    // ─── Argument lookup helpers ─────────────────────────────────────

    private static ScannedArgument? FindArg(IReadOnlyList<ScannedArgument> args, string paramName)
        => args.FirstOrDefault(a => a.ParameterName == paramName);

    private static ScannedArgument? FindArgByPosition(IReadOnlyList<ScannedArgument> args, int position)
        => args.FirstOrDefault(a => a.Position == position);

    /// <summary>
    /// Extract a string value from a chain link argument by parameter name or position.
    /// </summary>
    private static string? FindChainArgString(FluentChainWalker.ChainLink link, string paramName, int fallbackPosition)
    {
        foreach (var arg in link.Arguments)
        {
            var value = arg.Value.Accept(ValueExtractor.Instance, null);
            if (value is null) continue;

            var argParam = arg.Parameter?.Name;
            if (argParam == paramName || (argParam is null && arg == link.Arguments[fallbackPosition]))
            {
                if (value.TryGetString(out var s)) return s;
                if (value is OperationValue.Constant { Value: var v }) return v?.ToString();
                if (value is OperationValue.SymbolReference { Symbol: var sym }) return sym.Name;
            }
        }

        // Fallback: try by position
        if (fallbackPosition < link.Arguments.Count)
        {
            var value = link.Arguments[fallbackPosition].Value.Accept(ValueExtractor.Instance, null);
            if (value is not null && value.TryGetString(out var s)) return s;
            if (value is OperationValue.Constant { Value: var v }) return v?.ToString();
            if (value is OperationValue.SymbolReference { Symbol: var sym }) return sym.Name;
        }

        return null;
    }

    private static string StripEnumPrefix(string value)
    {
        var lastDot = value.LastIndexOf('.');
        return lastDot >= 0 ? value[(lastDot + 1)..] : value;
    }

    /// <summary>
    /// Produces the PO <c>#.</c> context comment: <c>.Translation</c>, <c>TranslationDefinitions.DefineSimple</c>, etc.
    /// </summary>
    private static string FormatContext(ScannedCallSite call)
    {
        if (call.MethodName.StartsWith(ExtensionsContract.DefinePrefix, StringComparison.Ordinal))
            return $"{nameof(BlazorLocalization.Extensions.Translation.Definitions.TranslationDefinitions)}.{call.MethodName}";

        return $".{call.MethodName}";
    }
}
