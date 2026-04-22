using BlazorLocalization.Extensions;
using BlazorLocalization.Extensions.Translation;
using BlazorLocalization.Extensions.Translation.Definitions;
using Microsoft.Extensions.Localization;

namespace BlazorLocalization.Extractor.Adapters.Roslyn;

/// <summary>
/// Single source of truth for every reference from the Extractor to the Extensions project.
/// Compiler-verified where C# allows it (<c>typeof</c>, <c>nameof</c>); documented string
/// constants where it doesn't (C#14 extension members, parameter names).
/// <para>
/// If anything in the Extensions project is renamed, this file either breaks the build
/// (for <c>typeof</c>/<c>nameof</c> references) or is the one place to update
/// (for string constants). The test <c>ExtensionsContractTests</c> validates the
/// parameter-name constants via reflection.
/// </para>
/// </summary>
internal static class ExtensionsContract
{
    // ── Metadata names (for Compilation.GetTypeByMetadataName) ────────

    /// <summary>Fully-qualified name of <see cref="IStringLocalizer"/>.</summary>
    public static readonly string MetaIStringLocalizer =
        typeof(IStringLocalizer).FullName!;

    /// <summary>Fully-qualified name of <see cref="BlazorLocalization.Extensions.TranslationAttribute"/>.</summary>
    public static readonly string MetaTranslationAttribute =
        typeof(TranslationAttribute).FullName!;

    /// <summary>Fully-qualified name of <see cref="TranslationDefinitions"/>.</summary>
    public static readonly string MetaTranslationDefinitions =
        typeof(TranslationDefinitions).FullName!;

    // ── Assembly reference ────────────────────────────────────────────

    /// <summary>Location of the Extensions assembly, needed as a compilation reference.</summary>
    public static readonly string ExtensionsAssemblyLocation =
        typeof(BlazorLocalization.Extensions.StringLocalizerExtensions).Assembly.Location;

    /// <summary>Fully-qualified name of <see cref="BlazorLocalization.Extensions.StringLocalizerExtensions"/>.</summary>
    public static readonly string MetaStringLocalizerExtensions =
        typeof(BlazorLocalization.Extensions.StringLocalizerExtensions).FullName!;

    // ── Extension method names ────────────────────────────────────────
    // C#14 `extension(IStringLocalizer)` block members cannot be referenced
    // via nameof — the methods live inside the extension block, not on the
    // containing static class. These string constants are the minimum fallback.

    /// <summary>The <c>.Translation()</c> extension method name.
    /// <see cref="BlazorLocalization.Extensions.StringLocalizerExtensions"/> — C#14 extension block member.</summary>
    public const string Translation = "Translation";

    /// <summary>The <c>.Display()</c> extension method name.
    /// <see cref="BlazorLocalization.Extensions.StringLocalizerExtensions"/> — C#14 extension block member.</summary>
    public const string Display = "Display";

    // ── Definition factory method names ───────────────────────────────

    /// <summary><see cref="TranslationDefinitions.DefineSimple"/></summary>
    public const string DefineSimple = nameof(TranslationDefinitions.DefineSimple);

    /// <summary><see cref="TranslationDefinitions.DefinePlural"/></summary>
    public const string DefinePlural = nameof(TranslationDefinitions.DefinePlural);

    /// <summary><see cref="TranslationDefinitions.DefineSelect{TSelect}"/></summary>
    public const string DefineSelect = nameof(TranslationDefinitions.DefineSelect);

    /// <summary><see cref="TranslationDefinitions.DefineSelectPlural{TSelect}"/></summary>
    public const string DefineSelectPlural = nameof(TranslationDefinitions.DefineSelectPlural);

    /// <summary>Shared prefix for all <c>Define*</c> factory methods.</summary>
    public const string DefinePrefix = "Define";

    // ── Builder / Definition type names (for FluentChainWalker) ──────

    public const string TypeSimpleBuilder = nameof(SimpleBuilder);
    public const string TypePluralBuilder = nameof(PluralBuilder);
    // Generic types: nameof(SelectBuilder<StringComparison>) yields "SelectBuilder"
    public const string TypeSelectBuilder = nameof(SelectBuilder<StringComparison>);
    public const string TypeSelectPluralBuilder = nameof(SelectPluralBuilder<StringComparison>);
    public const string TypeSimpleDefinition = nameof(SimpleDefinition);
    public const string TypePluralDefinition = nameof(PluralDefinition);
    public const string TypeSelectDefinition = nameof(SelectDefinition<StringComparison>);
    public const string TypeSelectPluralDefinition = nameof(SelectPluralDefinition<StringComparison>);

    // ── Metadata names for builder/definition types (for Compilation.GetTypeByMetadataName) ──

    public static readonly string MetaSimpleBuilder = typeof(SimpleBuilder).FullName!;
    public static readonly string MetaPluralBuilder = typeof(PluralBuilder).FullName!;
    // Generic open types use backtick notation: SelectBuilder`1
    public static readonly string MetaSelectBuilder = typeof(SelectBuilder<>).FullName!;
    public static readonly string MetaSelectPluralBuilder = typeof(SelectPluralBuilder<>).FullName!;
    public static readonly string MetaSimpleDefinition = typeof(SimpleDefinition).FullName!;
    public static readonly string MetaPluralDefinition = typeof(PluralDefinition).FullName!;
    public static readonly string MetaSelectDefinition = typeof(SelectDefinition<>).FullName!;
    public static readonly string MetaSelectPluralDefinition = typeof(SelectPluralDefinition<>).FullName!;

    // ── Chain method names ────────────────────────────────────────────

    /// <summary><see cref="SimpleBuilder.For"/>, <see cref="PluralBuilder.For"/>, etc.</summary>
    public const string ChainFor = nameof(SimpleBuilder.For);

    /// <summary><see cref="PluralBuilder.One"/></summary>
    public const string ChainOne = nameof(PluralBuilder.One);

    /// <summary><see cref="PluralBuilder.Other"/></summary>
    public const string ChainOther = nameof(PluralBuilder.Other);

    /// <summary><see cref="PluralBuilder.Zero"/></summary>
    public const string ChainZero = nameof(PluralBuilder.Zero);

    /// <summary><see cref="PluralBuilder.Two"/></summary>
    public const string ChainTwo = nameof(PluralBuilder.Two);

    /// <summary><see cref="PluralBuilder.Few"/></summary>
    public const string ChainFew = nameof(PluralBuilder.Few);

    /// <summary><see cref="PluralBuilder.Many"/></summary>
    public const string ChainMany = nameof(PluralBuilder.Many);

    /// <summary><see cref="PluralBuilder.Exactly"/></summary>
    public const string ChainExactly = nameof(PluralBuilder.Exactly);

    /// <summary><see cref="SelectBuilder{TSelect}.When"/></summary>
    public const string ChainWhen = nameof(SelectBuilder<StringComparison>.When);

    /// <summary><see cref="SelectBuilder{TSelect}.Otherwise"/></summary>
    public const string ChainOtherwise = nameof(SelectBuilder<StringComparison>.Otherwise);

    // ── [Translation] attribute property names ────────────────────────

    /// <summary><see cref="TranslationAttribute.Locale"/></summary>
    public const string AttrLocale = nameof(TranslationAttribute.Locale);

    /// <summary><see cref="TranslationAttribute.Key"/></summary>
    public const string AttrKey = nameof(TranslationAttribute.Key);

    // ── Parameter names ──────────────────────────────────────────────
    // C# does not support nameof for method parameters.
    // These constants match the parameter names in StringLocalizerExtensions
    // and the builder chain methods. Validated by ExtensionsContractTests.

    /// <summary>The <c>key</c> parameter on <see cref="TranslationDefinitions.DefineSimple"/>
    /// and all <c>.Translation()</c> overloads.</summary>
    public const string ParamKey = "key";

    /// <summary>The <c>name</c> parameter on <c>IStringLocalizer.this[string name]</c>.</summary>
    public const string ParamName = "name";

    /// <summary>The <c>message</c> parameter on builder chain methods like <see cref="PluralBuilder.One"/>.</summary>
    public const string ParamMessage = "message";

    /// <summary>The <c>sourceMessage</c> parameter on <see cref="TranslationDefinitions.DefineSimple"/>
    /// and the simple <c>.Translation()</c> overload.</summary>
    public const string ParamSourceMessage = "sourceMessage";

    /// <summary>The <c>howMany</c> parameter on plural <c>.Translation()</c> overloads.</summary>
    public const string ParamHowMany = "howMany";

    /// <summary>The <c>select</c> parameter on select <c>.Translation()</c> overloads
    /// and <see cref="SelectBuilder{TSelect}.When"/>.</summary>
    public const string ParamSelect = "select";

    /// <summary>The <c>ordinal</c> parameter on plural <c>.Translation()</c> overloads.</summary>
    public const string ParamOrdinal = "ordinal";

    /// <summary>The <c>locale</c> parameter on <see cref="SimpleBuilder.For"/>.</summary>
    public const string ParamLocale = "locale";

    /// <summary>The <c>value</c> parameter on <see cref="PluralBuilder.Exactly"/>.</summary>
    public const string ParamValue = "value";

    // ── Plural category names ────────────────────────────────────────
    // These serve double duty: they match both the builder method names
    // (PluralBuilder.Zero, .One, etc.) and the CLDR category identifiers.

    public static readonly HashSet<string> PluralCategoryNames =
    [
        ChainZero, ChainOne, ChainTwo, ChainFew, ChainMany, ChainOther
    ];
}
