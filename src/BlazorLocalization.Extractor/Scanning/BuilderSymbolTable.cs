using System.Reflection;
using BlazorLocalization.Extensions.Translation;
using BlazorLocalization.Extensions.Translation.Definitions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using StringLocalizerExtensions = BlazorLocalization.Extensions.StringLocalizerExtensions;

namespace BlazorLocalization.Extractor.Scanning;

/// <summary>
/// Pre-resolves all <c>BlazorLocalization.Extensions</c> types and methods as Roslyn symbols,
/// using <c>typeof()</c>, <c>nameof()</c>, and reflection. Zero hardcoded strings —
/// the compiler and reflection enforce the contract.
/// </summary>
/// <remarks>
/// Roslyn and reflection describe the same Extensions DLL: <c>typeof(PluralBuilder).FullName!</c>
/// is the bridge key for <c>CSharpCompilation.GetTypeByMetadataName()</c>. Parameter names
/// come from <c>MethodInfo.GetParameters()</c>, not string literals.
/// </remarks>
public sealed class BuilderSymbolTable
{
	// ── Builder type symbols ──────────────────────────────────────────

	public INamedTypeSymbol SimpleBuilder { get; }
	public INamedTypeSymbol PluralBuilder { get; }
	public INamedTypeSymbol SelectBuilder { get; }
	public INamedTypeSymbol SelectPluralBuilder { get; }

	// ── Definition type symbols ──────────────────────────────────

	public INamedTypeSymbol SimpleDefinition { get; }
	public INamedTypeSymbol PluralDefinition { get; }
	public INamedTypeSymbol SelectDefinition { get; }
	public INamedTypeSymbol SelectPluralDefinition { get; }

	// ── Definition factory class ─────────────────────────────────

	public INamedTypeSymbol TranslateClass { get; }

	// ── Attribute type symbol ─────────────────────────────────────────

	public INamedTypeSymbol TranslationAttribute { get; }

	/// <summary>Property name for <c>TranslationAttribute.Message</c> (constructor parameter).</summary>
	public string TranslationMessageParam { get; } = nameof(Extensions.TranslationAttribute.Message);

	/// <summary>Property name for <c>TranslationAttribute.Locale</c> (named argument).</summary>
	public string TranslationLocaleParam { get; } = nameof(Extensions.TranslationAttribute.Locale);

	/// <summary>Property name for <c>TranslationAttribute.Key</c> (named argument).</summary>
	public string TranslationKeyParam { get; } = nameof(Extensions.TranslationAttribute.Key);

	// ── Method symbols: PluralBuilder ─────────────────────────────────

	public IMethodSymbol PluralFor { get; }
	public IMethodSymbol PluralExactly { get; }
	public IMethodSymbol PluralZero { get; }
	public IMethodSymbol PluralOne { get; }
	public IMethodSymbol PluralTwo { get; }
	public IMethodSymbol PluralFew { get; }
	public IMethodSymbol PluralMany { get; }
	public IMethodSymbol PluralOther { get; }

	/// <summary>All CLDR category methods on PluralBuilder: Zero, One, Two, Few, Many, Other.</summary>
	public HashSet<IMethodSymbol> PluralCategoryMethods { get; }

	// ── Method symbols: PluralDefinition ─────────────────────────────

	public IMethodSymbol PluralDefFor { get; }
	public IMethodSymbol PluralDefExactly { get; }
	public IMethodSymbol PluralDefZero { get; }
	public IMethodSymbol PluralDefOne { get; }
	public IMethodSymbol PluralDefTwo { get; }
	public IMethodSymbol PluralDefFew { get; }
	public IMethodSymbol PluralDefMany { get; }
	public IMethodSymbol PluralDefOther { get; }

	/// <summary>All CLDR category methods on PluralDefinition: Zero, One, Two, Few, Many, Other.</summary>
	public HashSet<IMethodSymbol> PluralDefCategoryMethods { get; }

	// ── Method symbols: SimpleBuilder ─────────────────────────────────

	public IMethodSymbol SimpleFor { get; }

	// ── Method symbols: SimpleDefinition ──────────────────────────────

	public IMethodSymbol SimpleDefFor { get; }

	// ── Method symbols: SelectBuilder<> ───────────────────────────────

	public IMethodSymbol SelectWhen { get; }
	public IMethodSymbol SelectOtherwise { get; }
	public IMethodSymbol SelectFor { get; }

	// ── Method symbols: SelectPluralBuilder<> ─────────────────────────

	public IMethodSymbol SelectPluralWhen { get; }
	public IMethodSymbol SelectPluralOtherwise { get; }
	public IMethodSymbol SelectPluralFor { get; }
	public IMethodSymbol SelectPluralExactly { get; }
	public IMethodSymbol SelectPluralZero { get; }
	public IMethodSymbol SelectPluralOne { get; }
	public IMethodSymbol SelectPluralTwo { get; }
	public IMethodSymbol SelectPluralFew { get; }
	public IMethodSymbol SelectPluralMany { get; }
	public IMethodSymbol SelectPluralOther { get; }

	/// <summary>All CLDR category methods on SelectPluralBuilder: Zero, One, Two, Few, Many, Other.</summary>
	public HashSet<IMethodSymbol> SelectPluralCategoryMethods { get; }

	// ── Method symbols: SelectDefinition<> ───────────────────────────

	public IMethodSymbol SelectDefWhen { get; }
	public IMethodSymbol SelectDefOtherwise { get; }
	public IMethodSymbol SelectDefFor { get; }

	// ── Method symbols: SelectPluralDefinition<> ────────────────────

	public IMethodSymbol SelectPluralDefWhen { get; }
	public IMethodSymbol SelectPluralDefOtherwise { get; }
	public IMethodSymbol SelectPluralDefFor { get; }
	public IMethodSymbol SelectPluralDefExactly { get; }
	public IMethodSymbol SelectPluralDefZero { get; }
	public IMethodSymbol SelectPluralDefOne { get; }
	public IMethodSymbol SelectPluralDefTwo { get; }
	public IMethodSymbol SelectPluralDefFew { get; }
	public IMethodSymbol SelectPluralDefMany { get; }
	public IMethodSymbol SelectPluralDefOther { get; }

	/// <summary>All CLDR category methods on SelectPluralDefinition: Zero, One, Two, Few, Many, Other.</summary>
	public HashSet<IMethodSymbol> SelectPluralDefCategoryMethods { get; }

	// ── Combined category sets (Builder + DefinitionBuilder) ─────────

	/// <summary>All CLDR category methods on PluralBuilder and PluralDefinition.</summary>
	public HashSet<IMethodSymbol> AllPluralCategoryMethods { get; }

	/// <summary>All CLDR category methods on SelectPluralBuilder and SelectPluralDefinition.</summary>
	public HashSet<IMethodSymbol> AllSelectPluralCategoryMethods { get; }

	// ── Definition factory method symbols ────────────────────────

	public IMethodSymbol DefineSimple { get; }
	public IMethodSymbol DefinePlural { get; }
	public IMethodSymbol DefineSelect { get; }
	public IMethodSymbol DefineSelectPlural { get; }

	// ── Reflection-derived parameter names ────────────────────────────

	/// <summary>Parameter name for "message" on category methods (e.g. <c>One(string message)</c>).</summary>
	public string CategoryMessageParam { get; }

	/// <summary>Parameter name for "locale" on <c>For(string locale)</c>.</summary>
	public string PluralForLocaleParam { get; }

	/// <summary>Parameter name for "value" on <c>Exactly(int value, string message)</c>.</summary>
	public string ExactlyValueParam { get; }

	/// <summary>Parameter name for "message" on <c>Exactly(int value, string message)</c>.</summary>
	public string ExactlyMessageParam { get; }

	/// <summary>Parameter name for "locale" on <c>SimpleBuilder.For(string locale, string message)</c>.</summary>
	public string SimpleForLocaleParam { get; }

	/// <summary>Parameter name for "message" on <c>SimpleBuilder.For(string locale, string message)</c>.</summary>
	public string SimpleForMessageParam { get; }

	/// <summary>Parameter name for "select" on <c>SelectBuilder.When(TSelect select, string message)</c>.</summary>
	public string SelectWhenSelectParam { get; }

	/// <summary>Parameter name for "message" on <c>SelectBuilder.When(TSelect select, string message)</c>.</summary>
	public string SelectWhenMessageParam { get; }

	/// <summary>Parameter name for "message" on <c>SelectBuilder.Otherwise(string message)</c>.</summary>
	public string SelectOtherwiseMessageParam { get; }

	/// <summary>Parameter name for "locale" on <c>SelectBuilder.For(string locale)</c>.</summary>
	public string SelectForLocaleParam { get; }

	/// <summary>Parameter name for "select" on <c>SelectPluralBuilder.When(TSelect select)</c>.</summary>
	public string SelectPluralWhenSelectParam { get; }

	/// <summary>Parameter name for "locale" on <c>SelectPluralBuilder.For(string locale)</c>.</summary>
	public string SelectPluralForLocaleParam { get; }

	// ── Translate() overload parameter names ──────────────────────────

	/// <summary>Parameter name for "key" on all Translate() overloads.</summary>
	public string TranslateKeyParam { get; }

	/// <summary>Parameter name for "message" on simple Translate(key, message).</summary>
	public string TranslateMessageParam { get; }

	/// <summary>Parameter name for "howMany" on plural Translate(key, howMany).</summary>
	public string TranslateHowManyParam { get; }

	/// <summary>Parameter name for "ordinal" on plural Translate(key, howMany, ordinal).</summary>
	public string TranslateOrdinalParam { get; }

	/// <summary>Parameter name for "select" on select Translate(key, select).</summary>
	public string TranslateSelectParam { get; }

	// ── DefineXxx() factory parameter names ─────────────────────

	/// <summary>Parameter name for "key" on <c>Translations.DefineSimple(string key, string message)</c>.</summary>
	public string DefKeyParam { get; }

	/// <summary>Parameter name for "message" on <c>Translations.DefineSimple(string key, string message)</c>.</summary>
	public string DefSimpleMessageParam { get; }

	// ── Sentinel values ───────────────────────────────────────────────

	/// <summary>The internal sentinel used by <c>SelectBuilder</c> for the otherwise case.</summary>
	public string OtherwiseSentinel { get; }

	/// <summary>Entry-point method name — hardcoded because it's our own extension method.</summary>
	public string TranslateMethodName { get; } = nameof(StringLocalizerExtensions.Translation);

	public BuilderSymbolTable(CSharpCompilation compilation)
	{
		// ── Type resolution ───────────────────────────────────────────
		SimpleBuilder = ResolveType<Extensions.Translation.SimpleBuilder>(compilation);
		PluralBuilder = ResolveType<Extensions.Translation.PluralBuilder>(compilation);
		SelectBuilder = ResolveType(compilation, typeof(SelectBuilder<>));
		SelectPluralBuilder = ResolveType(compilation, typeof(SelectPluralBuilder<>));
		TranslationAttribute = ResolveType<Extensions.TranslationAttribute>(compilation);

		SimpleDefinition = ResolveType<Extensions.Translation.Definitions.SimpleDefinition>(compilation);
		PluralDefinition = ResolveType<Extensions.Translation.Definitions.PluralDefinition>(compilation);
		SelectDefinition = ResolveType(compilation, typeof(SelectDefinition<>));
		SelectPluralDefinition = ResolveType(compilation, typeof(SelectPluralDefinition<>));
		TranslateClass = ResolveType(compilation, typeof(Extensions.Translations));

		// ── PluralBuilder methods ─────────────────────────────────────
		PluralFor = ResolveMethod(PluralBuilder, nameof(Extensions.Translation.PluralBuilder.For));
		PluralExactly = ResolveMethod(PluralBuilder, nameof(Extensions.Translation.PluralBuilder.Exactly));
		PluralZero = ResolveMethod(PluralBuilder, nameof(Extensions.Translation.PluralBuilder.Zero));
		PluralOne = ResolveMethod(PluralBuilder, nameof(Extensions.Translation.PluralBuilder.One));
		PluralTwo = ResolveMethod(PluralBuilder, nameof(Extensions.Translation.PluralBuilder.Two));
		PluralFew = ResolveMethod(PluralBuilder, nameof(Extensions.Translation.PluralBuilder.Few));
		PluralMany = ResolveMethod(PluralBuilder, nameof(Extensions.Translation.PluralBuilder.Many));
		PluralOther = ResolveMethod(PluralBuilder, nameof(Extensions.Translation.PluralBuilder.Other));

		PluralCategoryMethods = new(SymbolEqualityComparer.Default)
		{
			PluralZero, PluralOne, PluralTwo, PluralFew, PluralMany, PluralOther
		};

		// ── SimpleBuilder methods ─────────────────────────────────────
		SimpleFor = ResolveMethod(SimpleBuilder, nameof(Extensions.Translation.SimpleBuilder.For));

		// ── SelectBuilder<> methods ───────────────────────────────────
		SelectWhen = ResolveMethod(SelectBuilder, nameof(SelectBuilder<DayOfWeek>.When));
		SelectOtherwise = ResolveMethod(SelectBuilder, nameof(SelectBuilder<DayOfWeek>.Otherwise));
		SelectFor = ResolveMethod(SelectBuilder, nameof(SelectBuilder<DayOfWeek>.For));

		// ── SelectPluralBuilder<> methods ─────────────────────────────
		SelectPluralWhen = ResolveMethod(SelectPluralBuilder, nameof(SelectPluralBuilder<DayOfWeek>.When));
		SelectPluralOtherwise = ResolveMethod(SelectPluralBuilder, nameof(SelectPluralBuilder<DayOfWeek>.Otherwise));
		SelectPluralFor = ResolveMethod(SelectPluralBuilder, nameof(SelectPluralBuilder<DayOfWeek>.For));
		SelectPluralExactly = ResolveMethod(SelectPluralBuilder, nameof(SelectPluralBuilder<DayOfWeek>.Exactly));
		SelectPluralZero = ResolveMethod(SelectPluralBuilder, nameof(SelectPluralBuilder<DayOfWeek>.Zero));
		SelectPluralOne = ResolveMethod(SelectPluralBuilder, nameof(SelectPluralBuilder<DayOfWeek>.One));
		SelectPluralTwo = ResolveMethod(SelectPluralBuilder, nameof(SelectPluralBuilder<DayOfWeek>.Two));
		SelectPluralFew = ResolveMethod(SelectPluralBuilder, nameof(SelectPluralBuilder<DayOfWeek>.Few));
		SelectPluralMany = ResolveMethod(SelectPluralBuilder, nameof(SelectPluralBuilder<DayOfWeek>.Many));
		SelectPluralOther = ResolveMethod(SelectPluralBuilder, nameof(SelectPluralBuilder<DayOfWeek>.Other));

		SelectPluralCategoryMethods = new(SymbolEqualityComparer.Default)
		{
			SelectPluralZero, SelectPluralOne, SelectPluralTwo,
			SelectPluralFew, SelectPluralMany, SelectPluralOther
		};

		// ── PluralDefinition methods ──────────────────────────────────
		PluralDefFor = ResolveMethod(PluralDefinition, nameof(Extensions.Translation.Definitions.PluralDefinition.For));
		PluralDefExactly = ResolveMethod(PluralDefinition, nameof(Extensions.Translation.Definitions.PluralDefinition.Exactly));
		PluralDefZero = ResolveMethod(PluralDefinition, nameof(Extensions.Translation.Definitions.PluralDefinition.Zero));
		PluralDefOne = ResolveMethod(PluralDefinition, nameof(Extensions.Translation.Definitions.PluralDefinition.One));
		PluralDefTwo = ResolveMethod(PluralDefinition, nameof(Extensions.Translation.Definitions.PluralDefinition.Two));
		PluralDefFew = ResolveMethod(PluralDefinition, nameof(Extensions.Translation.Definitions.PluralDefinition.Few));
		PluralDefMany = ResolveMethod(PluralDefinition, nameof(Extensions.Translation.Definitions.PluralDefinition.Many));
		PluralDefOther = ResolveMethod(PluralDefinition, nameof(Extensions.Translation.Definitions.PluralDefinition.Other));

		PluralDefCategoryMethods = new(SymbolEqualityComparer.Default)
		{
			PluralDefZero, PluralDefOne, PluralDefTwo, PluralDefFew, PluralDefMany, PluralDefOther
		};

		// ── SimpleDefinition methods ──────────────────────────────────
		SimpleDefFor = ResolveMethod(SimpleDefinition, nameof(Extensions.Translation.Definitions.SimpleDefinition.For));

		// ── SelectDefinition<> methods ────────────────────────────────
		SelectDefWhen = ResolveMethod(SelectDefinition, nameof(SelectDefinition<DayOfWeek>.When));
		SelectDefOtherwise = ResolveMethod(SelectDefinition, nameof(SelectDefinition<DayOfWeek>.Otherwise));
		SelectDefFor = ResolveMethod(SelectDefinition, nameof(SelectDefinition<DayOfWeek>.For));

		// ── SelectPluralDefinition<> methods ──────────────────────────
		SelectPluralDefWhen = ResolveMethod(SelectPluralDefinition, nameof(SelectPluralDefinition<DayOfWeek>.When));
		SelectPluralDefOtherwise = ResolveMethod(SelectPluralDefinition, nameof(SelectPluralDefinition<DayOfWeek>.Otherwise));
		SelectPluralDefFor = ResolveMethod(SelectPluralDefinition, nameof(SelectPluralDefinition<DayOfWeek>.For));
		SelectPluralDefExactly = ResolveMethod(SelectPluralDefinition, nameof(SelectPluralDefinition<DayOfWeek>.Exactly));
		SelectPluralDefZero = ResolveMethod(SelectPluralDefinition, nameof(SelectPluralDefinition<DayOfWeek>.Zero));
		SelectPluralDefOne = ResolveMethod(SelectPluralDefinition, nameof(SelectPluralDefinition<DayOfWeek>.One));
		SelectPluralDefTwo = ResolveMethod(SelectPluralDefinition, nameof(SelectPluralDefinition<DayOfWeek>.Two));
		SelectPluralDefFew = ResolveMethod(SelectPluralDefinition, nameof(SelectPluralDefinition<DayOfWeek>.Few));
		SelectPluralDefMany = ResolveMethod(SelectPluralDefinition, nameof(SelectPluralDefinition<DayOfWeek>.Many));
		SelectPluralDefOther = ResolveMethod(SelectPluralDefinition, nameof(SelectPluralDefinition<DayOfWeek>.Other));

		SelectPluralDefCategoryMethods = new(SymbolEqualityComparer.Default)
		{
			SelectPluralDefZero, SelectPluralDefOne, SelectPluralDefTwo,
			SelectPluralDefFew, SelectPluralDefMany, SelectPluralDefOther
		};

		// ── Combined category sets ────────────────────────────────────
		AllPluralCategoryMethods = new(PluralCategoryMethods, SymbolEqualityComparer.Default);
		AllPluralCategoryMethods.UnionWith(PluralDefCategoryMethods);

		AllSelectPluralCategoryMethods = new(SelectPluralCategoryMethods, SymbolEqualityComparer.Default);
		AllSelectPluralCategoryMethods.UnionWith(SelectPluralDefCategoryMethods);

		// ── Definition factory methods (each has a unique name) ──────
		DefineSimple = ResolveMethod(TranslateClass, nameof(Extensions.Translations.DefineSimple));
		DefinePlural = ResolveMethod(TranslateClass, nameof(Extensions.Translations.DefinePlural));
		DefineSelect = ResolveMethod(TranslateClass, nameof(Extensions.Translations.DefineSelect));
		DefineSelectPlural = ResolveMethod(TranslateClass, nameof(Extensions.Translations.DefineSelectPlural));

		// ── Reflection-derived parameter names ────────────────────────
		CategoryMessageParam = ReflectParam<Extensions.Translation.PluralBuilder>(nameof(Extensions.Translation.PluralBuilder.One), 0);
		PluralForLocaleParam = ReflectParam<Extensions.Translation.PluralBuilder>(nameof(Extensions.Translation.PluralBuilder.For), 0);
		ExactlyValueParam = ReflectParam<Extensions.Translation.PluralBuilder>(nameof(Extensions.Translation.PluralBuilder.Exactly), 0);
		ExactlyMessageParam = ReflectParam<Extensions.Translation.PluralBuilder>(nameof(Extensions.Translation.PluralBuilder.Exactly), 1);

		SimpleForLocaleParam = ReflectParam<Extensions.Translation.SimpleBuilder>(nameof(Extensions.Translation.SimpleBuilder.For), 0);
		SimpleForMessageParam = ReflectParam<Extensions.Translation.SimpleBuilder>(nameof(Extensions.Translation.SimpleBuilder.For), 1);

		SelectWhenSelectParam = ReflectParam(typeof(SelectBuilder<>), nameof(SelectBuilder<DayOfWeek>.When), 0);
		SelectWhenMessageParam = ReflectParam(typeof(SelectBuilder<>), nameof(SelectBuilder<DayOfWeek>.When), 1);
		SelectOtherwiseMessageParam = ReflectParam(typeof(SelectBuilder<>), nameof(SelectBuilder<DayOfWeek>.Otherwise), 0);
		SelectForLocaleParam = ReflectParam(typeof(SelectBuilder<>), nameof(SelectBuilder<DayOfWeek>.For), 0);

		SelectPluralWhenSelectParam = ReflectParam(typeof(SelectPluralBuilder<>), nameof(SelectPluralBuilder<DayOfWeek>.When), 0);
		SelectPluralForLocaleParam = ReflectParam(typeof(SelectPluralBuilder<>), nameof(SelectPluralBuilder<DayOfWeek>.For), 0);

		// ── Translate() parameter names (via reflection on StringLocalizerExtensions) ──
		// The simple overload: Translate(this IStringLocalizer, string key, string message, object? replaceWith)
		var simpleTranslate = typeof(StringLocalizerExtensions).GetMethods()
			.First(m => m.Name == nameof(StringLocalizerExtensions.Translation)
			            && !m.IsGenericMethod
			            && m.GetParameters().Any(p => p.ParameterType == typeof(string) && p.Position == 2));
		TranslateKeyParam = simpleTranslate.GetParameters()[1].Name!;   // skip 'this' param at [0]
		TranslateMessageParam = simpleTranslate.GetParameters()[2].Name!;

		// The plural overload: Translate(this IStringLocalizer, string key, int howMany, bool ordinal, object? replaceWith)
		var pluralTranslate = typeof(StringLocalizerExtensions).GetMethods()
			.First(m => m.Name == nameof(StringLocalizerExtensions.Translation)
			            && !m.IsGenericMethod
			            && m.GetParameters().Any(p => p.ParameterType == typeof(int)));
		TranslateHowManyParam = pluralTranslate.GetParameters()[2].Name!;

		// The ordinal parameter on the plural overload: Translate(this IStringLocalizer, string key, int howMany, bool ordinal, object? replaceWith)
		TranslateOrdinalParam = pluralTranslate.GetParameters()[3].Name!;

		// The select overload — generic, no int param
		var selectTranslate = typeof(StringLocalizerExtensions).GetMethods()
			.First(m => m.Name == nameof(StringLocalizerExtensions.Translation)
			            && m.IsGenericMethod
			            && !m.GetParameters().Any(p => p.ParameterType == typeof(int)));
		TranslateSelectParam = selectTranslate.GetParameters()[2].Name!;

		// ── DefineXxx() factory parameter names (via reflection on Translations) ──
		var defineSimpleReflect = typeof(Extensions.Translations).GetMethod(nameof(Extensions.Translations.DefineSimple))!;
		DefKeyParam = defineSimpleReflect.GetParameters()[0].Name!;
		DefSimpleMessageParam = defineSimpleReflect.GetParameters()[1].Name!;

		// ── Sentinel values ───────────────────────────────────────────
		OtherwiseSentinel = (string)typeof(SelectBuilder<>)
			.GetField("OtherwiseSentinel", BindingFlags.NonPublic | BindingFlags.Static)!
			.GetValue(null)!;

		// ── Cross-validation ──────────────────────────────────────────
		CrossValidate();
		CrossValidateTranslationAttribute();
		CrossValidateDefinitionBuilders();
		CrossValidateFactoryParams();
	}

	/// <summary>
	/// Determines the builder type for a <c>Translate()</c> call's return type.
	/// Returns <c>null</c> for unrecognized return types (e.g. indexer access).
	/// </summary>
	public BuilderKind? ClassifyReturnType(ITypeSymbol returnType)
	{
		var original = returnType.OriginalDefinition;

		if (SymbolEqualityComparer.Default.Equals(original, SimpleBuilder))
			return BuilderKind.Simple;
		if (SymbolEqualityComparer.Default.Equals(original, PluralBuilder))
			return BuilderKind.Plural;
		if (SymbolEqualityComparer.Default.Equals(original, SelectBuilder))
			return BuilderKind.Select;
		if (SymbolEqualityComparer.Default.Equals(original, SelectPluralBuilder))
			return BuilderKind.SelectPlural;

		if (SymbolEqualityComparer.Default.Equals(original, SimpleDefinition))
			return BuilderKind.Simple;
		if (SymbolEqualityComparer.Default.Equals(original, PluralDefinition))
			return BuilderKind.Plural;
		if (SymbolEqualityComparer.Default.Equals(original, SelectDefinition))
			return BuilderKind.Select;
		if (SymbolEqualityComparer.Default.Equals(original, SelectPluralDefinition))
			return BuilderKind.SelectPlural;

		return null;
	}

	/// <summary>
	/// Checks whether a chain method symbol matches any known builder method.
	/// Uses <see cref="IMethodSymbol.OriginalDefinition"/> so constructed generics
	/// (e.g. <c>SelectBuilder&lt;TestCategory&gt;.When</c>) match the open definition.
	/// </summary>
	public bool IsMethod(IMethodSymbol symbol, IMethodSymbol expected) =>
		SymbolEqualityComparer.Default.Equals(symbol.OriginalDefinition, expected);

	/// <summary>
	/// Checks whether a chain method symbol is a CLDR plural category method
	/// on either PluralBuilder or SelectPluralBuilder.
	/// </summary>
	public bool IsPluralCategory(IMethodSymbol symbol) =>
		PluralCategoryMethods.Contains(symbol.OriginalDefinition)
		|| SelectPluralCategoryMethods.Contains(symbol.OriginalDefinition);

	/// <summary>
	/// Checks whether an attribute on an enum field is <c>[Translation]</c>.
	/// </summary>
	public bool IsTranslationAttribute(INamedTypeSymbol? attributeClass) =>
		SymbolEqualityComparer.Default.Equals(attributeClass, TranslationAttribute);

	// ── Combined matchers (Builder + DefinitionBuilder) ──────────────

	public bool IsSimpleFor(IMethodSymbol symbol) =>
		IsMethod(symbol, SimpleFor) || IsMethod(symbol, SimpleDefFor);

	public bool IsPluralFor(IMethodSymbol symbol) =>
		IsMethod(symbol, PluralFor) || IsMethod(symbol, PluralDefFor);

	public bool IsPluralExactly(IMethodSymbol symbol) =>
		IsMethod(symbol, PluralExactly) || IsMethod(symbol, PluralDefExactly);

	public bool IsSelectWhen(IMethodSymbol symbol) =>
		IsMethod(symbol, SelectWhen) || IsMethod(symbol, SelectDefWhen);

	public bool IsSelectOtherwise(IMethodSymbol symbol) =>
		IsMethod(symbol, SelectOtherwise) || IsMethod(symbol, SelectDefOtherwise);

	public bool IsSelectFor(IMethodSymbol symbol) =>
		IsMethod(symbol, SelectFor) || IsMethod(symbol, SelectDefFor);

	public bool IsSelectPluralWhen(IMethodSymbol symbol) =>
		IsMethod(symbol, SelectPluralWhen) || IsMethod(symbol, SelectPluralDefWhen);

	public bool IsSelectPluralOtherwise(IMethodSymbol symbol) =>
		IsMethod(symbol, SelectPluralOtherwise) || IsMethod(symbol, SelectPluralDefOtherwise);

	public bool IsSelectPluralFor(IMethodSymbol symbol) =>
		IsMethod(symbol, SelectPluralFor) || IsMethod(symbol, SelectPluralDefFor);

	public bool IsSelectPluralExactly(IMethodSymbol symbol) =>
		IsMethod(symbol, SelectPluralExactly) || IsMethod(symbol, SelectPluralDefExactly);

	/// <summary>
	/// Checks whether a method symbol is one of the <c>Translations.DefineXxx()</c> factory methods.
	/// </summary>
	public bool IsDefinitionFactory(IMethodSymbol symbol) =>
		IsMethod(symbol, DefineSimple) || IsMethod(symbol, DefinePlural)
		|| IsMethod(symbol, DefineSelect) || IsMethod(symbol, DefineSelectPlural);

	// ── Private helpers ───────────────────────────────────────────────

	private static INamedTypeSymbol ResolveType<T>(CSharpCompilation compilation) =>
		compilation.GetTypeByMetadataName(typeof(T).FullName!)
		?? throw new InvalidOperationException(
			$"Roslyn cannot resolve {typeof(T).FullName}. " +
			"Is BlazorLocalization.Extensions included in the compilation references?");

	private static INamedTypeSymbol ResolveType(CSharpCompilation compilation, Type openGenericType) =>
		compilation.GetTypeByMetadataName(openGenericType.FullName!)
		?? throw new InvalidOperationException(
			$"Roslyn cannot resolve {openGenericType.FullName}. " +
			"Is BlazorLocalization.Extensions included in the compilation references?");

	private static IMethodSymbol ResolveMethod(INamedTypeSymbol type, string methodName)
	{
		var methods = type.GetMembers(methodName).OfType<IMethodSymbol>().ToList();
		return methods.Count switch
		{
			1 => methods[0],
			0 => throw new InvalidOperationException(
				$"Roslyn cannot find method '{methodName}' on {type.ToDisplayString()}."),
			_ => throw new InvalidOperationException(
				$"Expected 1 method '{methodName}' on {type.ToDisplayString()}, found {methods.Count}.")
		};
	}

	private static string ReflectParam<T>(string methodName, int position) =>
		typeof(T).GetMethod(methodName)!.GetParameters()[position].Name!;

	private static string ReflectParam(Type type, string methodName, int position) =>
		type.GetMethod(methodName)!.GetParameters()[position].Name!;

	/// <summary>
	/// Cross-validates Roslyn symbols against .NET reflection. Fails fast if they disagree.
	/// </summary>
	private void CrossValidate()
	{
		ValidateParams(PluralFor, typeof(Extensions.Translation.PluralBuilder), nameof(Extensions.Translation.PluralBuilder.For));
		ValidateParams(PluralExactly, typeof(Extensions.Translation.PluralBuilder), nameof(Extensions.Translation.PluralBuilder.Exactly));
		ValidateParams(PluralOne, typeof(Extensions.Translation.PluralBuilder), nameof(Extensions.Translation.PluralBuilder.One));

		ValidateParams(SimpleFor, typeof(Extensions.Translation.SimpleBuilder), nameof(Extensions.Translation.SimpleBuilder.For));

		ValidateParams(SelectWhen, typeof(SelectBuilder<>), nameof(SelectBuilder<DayOfWeek>.When));
		ValidateParams(SelectOtherwise, typeof(SelectBuilder<>), nameof(SelectBuilder<DayOfWeek>.Otherwise));
		ValidateParams(SelectFor, typeof(SelectBuilder<>), nameof(SelectBuilder<DayOfWeek>.For));

		ValidateParams(SelectPluralWhen, typeof(SelectPluralBuilder<>), nameof(SelectPluralBuilder<DayOfWeek>.When));
		ValidateParams(SelectPluralOtherwise, typeof(SelectPluralBuilder<>), nameof(SelectPluralBuilder<DayOfWeek>.Otherwise));
		ValidateParams(SelectPluralFor, typeof(SelectPluralBuilder<>), nameof(SelectPluralBuilder<DayOfWeek>.For));
		ValidateParams(SelectPluralExactly, typeof(SelectPluralBuilder<>), nameof(SelectPluralBuilder<DayOfWeek>.Exactly));
		ValidateParams(SelectPluralOne, typeof(SelectPluralBuilder<>), nameof(SelectPluralBuilder<DayOfWeek>.One));
	}

	private static void ValidateParams(IMethodSymbol roslynMethod, Type reflectionType, string methodName)
	{
		var reflectMethod = reflectionType.GetMethod(methodName)
			?? throw new InvalidOperationException(
				$"Reflection cannot find '{methodName}' on {reflectionType.FullName}.");

		var roslynParams = roslynMethod.Parameters;
		var reflectParams = reflectMethod.GetParameters();

		if (roslynParams.Length != reflectParams.Length)
			throw new InvalidOperationException(
				$"Parameter count mismatch for {reflectionType.Name}.{methodName}: " +
				$"Roslyn={roslynParams.Length}, Reflection={reflectParams.Length}.");

		for (var i = 0; i < roslynParams.Length; i++)
		{
			if (roslynParams[i].Name != reflectParams[i].Name)
				throw new InvalidOperationException(
					$"Parameter name mismatch for {reflectionType.Name}.{methodName}[{i}]: " +
					$"Roslyn='{roslynParams[i].Name}', Reflection='{reflectParams[i].Name}'.");
		}
	}

	/// <summary>
	/// Like <see cref="ValidateParams"/> but resolves the correct overload by parameter count and
	/// generic status.
	/// </summary>
	private static void ValidateFactoryParams(IMethodSymbol roslynMethod, Type reflectionType, string methodName, int paramCount, bool isGeneric)
	{
		var reflectMethod = reflectionType.GetMethods()
			.FirstOrDefault(m => m.Name == methodName && m.GetParameters().Length == paramCount && m.IsGenericMethod == isGeneric)
			?? throw new InvalidOperationException(
				$"Reflection cannot find '{methodName}({paramCount} params, generic={isGeneric})' on {reflectionType.FullName}.");

		var roslynParams = roslynMethod.Parameters;
		var reflectParams = reflectMethod.GetParameters();

		if (roslynParams.Length != reflectParams.Length)
			throw new InvalidOperationException(
				$"Parameter count mismatch for {reflectionType.Name}.{methodName}: " +
				$"Roslyn={roslynParams.Length}, Reflection={reflectParams.Length}.");

		for (var i = 0; i < roslynParams.Length; i++)
		{
			if (roslynParams[i].Name != reflectParams[i].Name)
				throw new InvalidOperationException(
					$"Parameter name mismatch for {reflectionType.Name}.{methodName}[{i}]: " +
					$"Roslyn='{roslynParams[i].Name}', Reflection='{reflectParams[i].Name}'.");
		}
	}

	/// <summary>
	/// Verifies that the Roslyn-resolved <c>TranslationAttribute</c> has the expected properties.
	/// </summary>
	private void CrossValidateTranslationAttribute()
	{
		var reflectionProps = typeof(Extensions.TranslationAttribute)
			.GetProperties(BindingFlags.Public | BindingFlags.Instance)
			.Select(p => p.Name)
			.ToHashSet();

		var roslynProps = TranslationAttribute
			.GetMembers()
			.OfType<IPropertySymbol>()
			.Where(p => p.DeclaredAccessibility == Accessibility.Public)
			.Select(p => p.Name)
			.ToHashSet();

		var expected = new[] { TranslationMessageParam, TranslationLocaleParam, TranslationKeyParam };
		foreach (var name in expected)
		{
			if (!reflectionProps.Contains(name))
				throw new InvalidOperationException(
					$"Reflection cannot find property '{name}' on {typeof(Extensions.TranslationAttribute).FullName}.");
			if (!roslynProps.Contains(name))
				throw new InvalidOperationException(
					$"Roslyn cannot find property '{name}' on {TranslationAttribute.ToDisplayString()}.");
		}
	}

	/// <summary>
	/// Cross-validates definition Roslyn symbols against .NET reflection.
	/// </summary>
	private void CrossValidateDefinitionBuilders()
	{
		ValidateParams(SimpleDefFor, typeof(Extensions.Translation.Definitions.SimpleDefinition), nameof(Extensions.Translation.Definitions.SimpleDefinition.For));

		ValidateParams(PluralDefFor, typeof(Extensions.Translation.Definitions.PluralDefinition), nameof(Extensions.Translation.Definitions.PluralDefinition.For));
		ValidateParams(PluralDefExactly, typeof(Extensions.Translation.Definitions.PluralDefinition), nameof(Extensions.Translation.Definitions.PluralDefinition.Exactly));
		ValidateParams(PluralDefOne, typeof(Extensions.Translation.Definitions.PluralDefinition), nameof(Extensions.Translation.Definitions.PluralDefinition.One));

		ValidateParams(SelectDefWhen, typeof(SelectDefinition<>), nameof(SelectDefinition<DayOfWeek>.When));
		ValidateParams(SelectDefOtherwise, typeof(SelectDefinition<>), nameof(SelectDefinition<DayOfWeek>.Otherwise));
		ValidateParams(SelectDefFor, typeof(SelectDefinition<>), nameof(SelectDefinition<DayOfWeek>.For));

		ValidateParams(SelectPluralDefWhen, typeof(SelectPluralDefinition<>), nameof(SelectPluralDefinition<DayOfWeek>.When));
		ValidateParams(SelectPluralDefOtherwise, typeof(SelectPluralDefinition<>), nameof(SelectPluralDefinition<DayOfWeek>.Otherwise));
		ValidateParams(SelectPluralDefFor, typeof(SelectPluralDefinition<>), nameof(SelectPluralDefinition<DayOfWeek>.For));
		ValidateParams(SelectPluralDefExactly, typeof(SelectPluralDefinition<>), nameof(SelectPluralDefinition<DayOfWeek>.Exactly));
		ValidateParams(SelectPluralDefOne, typeof(SelectPluralDefinition<>), nameof(SelectPluralDefinition<DayOfWeek>.One));

		ValidateParams(DefineSimple, typeof(Extensions.Translations), nameof(Extensions.Translations.DefineSimple));
		ValidateParams(DefinePlural, typeof(Extensions.Translations), nameof(Extensions.Translations.DefinePlural));
	}

	/// <summary>
	/// Verifies that Translations.DefineSimple factory param names match StringLocalizerExtensions.Translation
	/// param names, so shared chain interpretation logic works for both code paths.
	/// </summary>
	private void CrossValidateFactoryParams()
	{
		if (DefKeyParam != TranslateKeyParam)
			throw new InvalidOperationException(
				$"Translations.DefineSimple 'key' param '{DefKeyParam}' doesn't match " +
				$"StringLocalizerExtensions.Translation 'key' param '{TranslateKeyParam}'.");
		if (DefSimpleMessageParam != TranslateMessageParam)
			throw new InvalidOperationException(
				$"Translations.DefineSimple 'message' param '{DefSimpleMessageParam}' doesn't match " +
				$"StringLocalizerExtensions.Translation 'message' param '{TranslateMessageParam}'.");
	}
}

/// <summary>
/// Classifies which builder type a <c>Translate()</c> overload returns.
/// </summary>
public enum BuilderKind
{
	Simple,
	Plural,
	Select,
	SelectPlural
}
