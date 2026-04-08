using System.Reflection;
using BlazorLocalization.Extensions;
using BlazorLocalization.Extensions.Translation;
using BlazorLocalization.Extensions.Translation.Definitions;
using BlazorLocalization.Extractor.Adapters.Roslyn;
using FluentAssertions;

namespace BlazorLocalization.Extractor.Tests;

/// <summary>
/// Validates that the parameter-name string constants in <see cref="ExtensionsContract"/>
/// match the actual parameter names in the Extensions project's public API.
/// These can't use <c>nameof</c> (C# doesn't support it for parameters), so this
/// reflection-based test is the safety net against silent renames.
/// </summary>
public class ExtensionsContractTests
{
    [Fact]
    public void Translation_Simple_ParameterNames()
    {
        // SimpleBuilder Translation(string key, string message, object? replaceWith = null)
        var method = GetExtensionMethod("Translation", typeof(string), typeof(string));
        method.Should().NotBeNull("SimpleBuilder Translation(key, message) overload should exist");
        AssertParam(method!, ExtensionsContract.ParamKey);
        AssertParam(method!, ExtensionsContract.ParamMessage);
    }

    [Fact]
    public void Translation_Plural_ParameterNames()
    {
        // PluralBuilder Translation(string key, int howMany, bool ordinal = false, ...)
        var method = GetExtensionMethod("Translation", typeof(string), typeof(int));
        method.Should().NotBeNull("PluralBuilder Translation(key, howMany) overload should exist");
        AssertParam(method!, ExtensionsContract.ParamKey);
        AssertParam(method!, ExtensionsContract.ParamHowMany);
        AssertParam(method!, ExtensionsContract.ParamOrdinal);
    }

    [Fact]
    public void DefineSimple_ParameterNames()
    {
        var method = typeof(TranslationDefinitions).GetMethod(
            ExtensionsContract.DefineSimple,
            [typeof(string), typeof(string)]);
        method.Should().NotBeNull();
        AssertParam(method!, ExtensionsContract.ParamKey);
        AssertParam(method!, ExtensionsContract.ParamMessage);
    }

    [Fact]
    public void DefinePlural_ParameterNames()
    {
        var method = typeof(TranslationDefinitions).GetMethod(
            ExtensionsContract.DefinePlural,
            [typeof(string)]);
        method.Should().NotBeNull();
        AssertParam(method!, ExtensionsContract.ParamKey);
    }

    [Fact]
    public void SimpleBuilder_For_ParameterNames()
    {
        var method = typeof(SimpleBuilder).GetMethod(
            ExtensionsContract.ChainFor,
            [typeof(string), typeof(string)]);
        method.Should().NotBeNull();
        AssertParam(method!, ExtensionsContract.ParamLocale);
        AssertParam(method!, ExtensionsContract.ParamMessage);
    }

    [Fact]
    public void PluralBuilder_Exactly_ParameterNames()
    {
        var method = typeof(PluralBuilder).GetMethod(
            ExtensionsContract.ChainExactly,
            [typeof(int), typeof(string)]);
        method.Should().NotBeNull();
        AssertParam(method!, ExtensionsContract.ParamValue);
        AssertParam(method!, ExtensionsContract.ParamMessage);
    }

    [Fact]
    public void PluralBuilder_Category_ParameterNames()
    {
        // All category methods (One, Other, Zero, etc.) take a single 'message' param
        foreach (var name in new[] { "One", "Other", "Zero", "Two", "Few", "Many" })
        {
            var method = typeof(PluralBuilder).GetMethod(name, [typeof(string)]);
            method.Should().NotBeNull($"PluralBuilder.{name}(string) should exist");
            AssertParam(method!, ExtensionsContract.ParamMessage);
        }
    }

    [Fact]
    public void TranslationAttribute_PropertyNames()
    {
        typeof(TranslationAttribute).GetProperty(ExtensionsContract.AttrLocale)
            .Should().NotBeNull();
        typeof(TranslationAttribute).GetProperty(ExtensionsContract.AttrKey)
            .Should().NotBeNull();
    }

    // ── Helpers ──────────────────────────────────────────────────────

    /// <summary>
    /// Finds a <c>Translation</c> extension method by matching two parameter types.
    /// C#14 extension block methods may compile with the receiver as param 0 or not —
    /// so we search for any two params matching the given types regardless of position.
    /// </summary>
    private static MethodInfo? GetExtensionMethod(string name, Type firstArgType, Type secondArgType)
    {
        return typeof(BlazorLocalization.Extensions.StringLocalizerExtensions)
            .GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Instance)
            .Where(m => m.Name == name && !m.IsGenericMethod)
            .FirstOrDefault(m =>
            {
                var p = m.GetParameters();
                return p.Any(pp => pp.Name == "key" && pp.ParameterType == firstArgType)
                    && p.Any(pp => pp.ParameterType == secondArgType && pp.Name != "key");
            });
    }

    private static void AssertParam(MethodInfo method, string expectedParamName)
    {
        method.GetParameters()
            .Select(p => p.Name)
            .Should().Contain(expectedParamName,
                $"{method.DeclaringType!.Name}.{method.Name} should have parameter '{expectedParamName}'");
    }
}
