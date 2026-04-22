using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using BlazorLocalization.Analyzers.Scanning;
using BlazorLocalization.Analyzers.Scanning.TranslationFiles;
using BlazorLocalization.Analyzers.Scanning.TranslationFiles.Parsers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace BlazorLocalization.Analyzers.Analyzers;

/// <summary>
/// BL0003: Detects duplicate translation keys across the compilation.
/// BL0005: Detects key-only Translation() calls whose key has no definition.
/// BL0006: Detects conflicting values for the same key across translation files.
/// Collects all Translation()/Define*() keys during syntax analysis and reports
/// conflicts immediately when a second (conflicting) usage of a key is encountered.
/// Key-only references without a matching definition are reported at compilation end.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class TranslationKeyAnalyzer : DiagnosticAnalyzer
{
    private static readonly IReadOnlyList<ITranslationFileParser> Parsers =
        new ITranslationFileParser[] { new ResxTranslationFileParser() };

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } =
        ImmutableArray.Create(
            DiagnosticDescriptors.DuplicateKey,
            DiagnosticDescriptors.UndefinedKey,
            DiagnosticDescriptors.TranslationFileConflict);

    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(
            GeneratedCodeAnalysisFlags.Analyze | GeneratedCodeAnalysisFlags.ReportDiagnostics);

        context.RegisterCompilationStartAction(compilationContext =>
        {
            var msSymbols = new MicrosoftLocalizationSymbols(compilationContext.Compilation);
            if (msSymbols.IStringLocalizerType is null)
                return;

            var blSymbols = new BlazorLocalizationSymbols(compilationContext.Compilation);

            // Only fire in projects that use ProviderBasedStringLocalizerFactory
            // (flat key namespace — duplicates are real conflicts)
            if (blSymbols.ProviderBasedFactoryType is null)
                return;

            // Build translation file lookup from AdditionalFiles (resx, future: po/json)
            var lookup = TranslationFileLookup.Build(
                compilationContext.Options.AdditionalFiles, Parsers, compilationContext.CancellationToken);

            var keyRegistry = new ConcurrentDictionary<string, ConcurrentBag<KeyUsage>>();
            var reportedLocations = new ConcurrentDictionary<(string Key, Location Location), byte>();

            // BL0005: track defined keys vs key-only references
            var definedKeys = new ConcurrentDictionary<string, byte>();
            var keyOnlyUsages = new ConcurrentDictionary<string, ConcurrentBag<Location>>();

            compilationContext.RegisterSyntaxNodeAction(
                ctx => AnalyzeInvocation(ctx, msSymbols, blSymbols, keyRegistry, reportedLocations,
                    definedKeys, keyOnlyUsages, lookup),
                SyntaxKind.InvocationExpression);

            compilationContext.RegisterCompilationEndAction(endCtx =>
            {
                // BL0005: report key-only references with no definition
                foreach (var kvp in keyOnlyUsages)
                {
                    if (definedKeys.ContainsKey(kvp.Key))
                        continue;

                    foreach (var location in kvp.Value)
                    {
                        endCtx.ReportDiagnostic(
                            Diagnostic.Create(DiagnosticDescriptors.UndefinedKey, location, kvp.Key));
                    }
                }

                // BL0006: report translation file conflicts
                foreach (var conflict in lookup.Conflicts)
                {
                    var cultureDesc = conflict.Culture is null
                        ? "source text"
                        : $"translation for culture '{conflict.Culture}'";

                    var valueList = string.Join(" vs ",
                        conflict.ConflictingSources.Select(s =>
                            $"\"{s.Value}\" ({Path.GetFileName(s.FilePath)})"));

                    endCtx.ReportDiagnostic(
                        Diagnostic.Create(
                            DiagnosticDescriptors.TranslationFileConflict,
                            Location.None,
                            conflict.Key,
                            cultureDesc,
                            valueList));
                }
            });
        });
    }

    private static void AnalyzeInvocation(
        SyntaxNodeAnalysisContext context,
        MicrosoftLocalizationSymbols msSymbols,
        BlazorLocalizationSymbols blSymbols,
        ConcurrentDictionary<string, ConcurrentBag<KeyUsage>> keyRegistry,
        ConcurrentDictionary<(string Key, Location Location), byte> reportedLocations,
        ConcurrentDictionary<string, byte> definedKeys,
        ConcurrentDictionary<string, ConcurrentBag<Location>> keyOnlyUsages,
        TranslationFileLookup lookup)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;

        if (invocation.Expression is not MemberAccessExpressionSyntax memberAccess)
            return;

        var methodName = memberAccess.Name.Identifier.ValueText;

        // Classify static Define*() calls by method name (nameof-safe, distinct methods)
        KeyUsageKind? kind = null;
        bool isStatic = false;
        bool isTranslationCall = false;

        switch (methodName)
        {
            case BlazorLocalizationSymbols.DefineSimpleMethodName:
                kind = KeyUsageKind.Simple; isStatic = true; break;
            case BlazorLocalizationSymbols.DefinePluralMethodName:
                kind = KeyUsageKind.Plural; isStatic = true; break;
            case BlazorLocalizationSymbols.DefineSelectMethodName:
                kind = KeyUsageKind.Select; isStatic = true; break;
            case BlazorLocalizationSymbols.DefineSelectPluralMethodName:
                kind = KeyUsageKind.SelectPlural; isStatic = true; break;
            case BlazorLocalizationSymbols.TranslationMethodName:
                isTranslationCall = true; break;
            default:
                return;
        }

        // For inline Translation() calls, verify receiver is IStringLocalizer
        if (isTranslationCall)
        {
            var receiverType = context.SemanticModel.GetTypeInfo(
                memberAccess.Expression, context.CancellationToken).Type;
            if (!msSymbols.IsStringLocalizerType(receiverType))
                return;
        }
        else
        {
            // For static Define*() calls, verify receiver is TranslationDefinitions
            if (blSymbols.TranslationDefinitionsType is not null)
            {
                var receiverType = context.SemanticModel.GetTypeInfo(
                    memberAccess.Expression, context.CancellationToken).Type;
                if (!SymbolEqualityComparer.Default.Equals(receiverType, blSymbols.TranslationDefinitionsType))
                    return;
            }
        }

        // Resolve the method symbol
        if (context.SemanticModel.GetSymbolInfo(invocation, context.CancellationToken).Symbol
            is not IMethodSymbol method)
            return;

        // For Translation() calls, derive kind from return type (not method name)
        if (isTranslationCall)
            kind = blSymbols.GetBuilderKind(method.ReturnType) ?? KeyUsageKind.Simple;

        if (!TranslationCallExtractor.TryGetKeyFromInvocation(invocation, method, out var key, out _) || key is null)
            return;

        // Definition-bearing overloads have a string parameter at position 1 (the message).
        // Key-only overloads have either no second parameter or a non-string second parameter.
        var isDefinitionBearing = !isStatic
            && method.Parameters.Length >= 2
            && method.Parameters[1].Type.SpecialType == SpecialType.System_String;

        // Extract message for definition-bearing Translation() calls (for BL0003 conflict detection)
        string? message = null;
        if (isDefinitionBearing)
        {
            TranslationCallExtractor.TryGetMessageFromInvocation(invocation, method, out message);
        }

        // BL0005: track whether this call defines the key or only references it
        bool isKeyOnly = false;
        if (isStatic)
        {
            // Define*() calls always create definitions
            definedKeys.TryAdd(key, 0);
        }
        else if (isDefinitionBearing)
        {
            definedKeys.TryAdd(key, 0);
        }
        else
        {
            var keyOnlyBag = keyOnlyUsages.GetOrAdd(key, _ => new ConcurrentBag<Location>());
            keyOnlyBag.Add(invocation.GetLocation());
            isKeyOnly = true;
        }

        // BL0003: only definitions participate in duplicate detection —
        // key-only references are just consumers, not conflicting sources
        if (isKeyOnly)
            return;

        var usage = new KeyUsage(invocation.GetLocation(), message, kind!.Value, isStatic);
        var bag = keyRegistry.GetOrAdd(key, _ => new ConcurrentBag<KeyUsage>());
        bag.Add(usage);

        // Check for conflict immediately
        ReportIfConflict(context, key, bag, reportedLocations, lookup);
    }

    private static void ReportIfConflict(
        SyntaxNodeAnalysisContext context,
        string key,
        ConcurrentBag<KeyUsage> bag,
        ConcurrentDictionary<(string Key, Location Location), byte> reportedLocations,
        TranslationFileLookup lookup)
    {
        // Snapshot the bag into a deterministic order
        var usages = bag
            .OrderBy(u => u.Location.GetLineSpan().Path, StringComparer.Ordinal)
            .ThenBy(u => u.Location.GetLineSpan().StartLinePosition.Line)
            .ThenBy(u => u.Location.GetLineSpan().StartLinePosition.Character)
            .ToArray();

        if (usages.Length < 2)
            return;

        // Skip if all usages are inline with the same message (same-key-same-message reuse)
        if (usages.All(u => !u.IsStatic) && AllSameMessage(usages))
            return;

        var reason = ClassifyConflict(usages);

        // BL0003 enrichment: if resx data is available, append context
        if (lookup.TryGet(key, out var resxEntry) && resxEntry.SourceText is not null)
            reason += $" (resource file says: \"{resxEntry.SourceText}\")";

        // Report each usage that hasn't been reported yet (dedup by key + location)
        for (var i = 0; i < usages.Length; i++)
        {
            if (!reportedLocations.TryAdd((key, usages[i].Location), 0))
                continue;

            // Collect other locations as additional locations for IDE navigation
            var additionalLocations = usages
                .Where((_, idx) => idx != i)
                .Select(u => u.Location)
                .ToList();

            context.ReportDiagnostic(
                Diagnostic.Create(
                    DiagnosticDescriptors.DuplicateKey,
                    usages[i].Location,
                    additionalLocations,
                    properties: null,
                    key,
                    reason));
        }
    }

    private static string ClassifyConflict(KeyUsage[] usages)
    {
        var hasStatic = usages.Any(u => u.IsStatic);
        var hasInline = usages.Any(u => !u.IsStatic);
        var distinctKinds = usages.Select(u => u.Kind).Distinct().Count();

        if (distinctKinds > 1)
            return "type mismatch — defined as both " +
                   string.Join(" and ", usages.Select(u => u.Kind).Distinct());

        if (hasStatic && hasInline)
            return "has both a static definition and inline usage";

        if (hasStatic)
            return "is defined multiple times";

        // All inline with different messages
        return "has conflicting messages";
    }

    private static bool AllSameMessage(KeyUsage[] usages)
    {
        if (usages.Length == 0)
            return true;

        var first = usages[0].Message;
        return usages.All(u => u.Message == first);
    }
}
