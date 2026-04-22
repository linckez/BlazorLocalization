using System.Reflection;
using BlazorLocalization.Analyzers.Scanning;
using BlazorLocalization.Extensions.Translation;
using FluentAssertions;
using Microsoft.Extensions.Localization;

namespace BlazorLocalization.Analyzers.Tests;

using BLExtensions = BlazorLocalization.Extensions.StringLocalizerExtensions;
using MSExtensions = Microsoft.Extensions.Localization.StringLocalizerExtensions;

/// <summary>
/// Contract tests that verify the string constants in the analyzer symbol tables
/// match the real method and parameter names in the referenced assemblies.
/// Only covers values that can't use typeof()/nameof() — extension method names and parameter names.
/// Type metadata names and DefineXxx method names are compile-time safe via typeof()/nameof().
/// </summary>
public class SymbolNameContractTests
{
    // ── Microsoft.Extensions.Localization ──────────────────────────────

    [Fact]
    public void GetStringMethodName_MatchesIStringLocalizerExtensions()
    {
        var methods = typeof(MSExtensions).GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Where(m => m.Name == MicrosoftLocalizationSymbols.GetStringMethodName)
            .ToList();

        methods.Should().NotBeEmpty(
            $"StringLocalizerExtensions should have a method named '{MicrosoftLocalizationSymbols.GetStringMethodName}'");
    }

    [Fact]
    public void NameParameterName_MatchesGetStringParameter()
    {
        var method = typeof(MSExtensions).GetMethods(BindingFlags.Public | BindingFlags.Static)
            .First(m => m.Name == MicrosoftLocalizationSymbols.GetStringMethodName
                        && m.GetParameters().Length == 2 // (this IStringLocalizer, string name)
                        && m.GetParameters()[1].ParameterType == typeof(string));

        var param = method.GetParameters()[1]; // skip 'this' param
        param.Name.Should().Be(MicrosoftLocalizationSymbols.NameParameterName,
            "the first real parameter of GetString should be 'name'");
    }

    // ── BlazorLocalization.Extensions (Translation method) ────────────

    [Fact]
    public void TranslationMethodName_MatchesExtensionMethod()
    {
        // C# 14 extension blocks compile to a static class; find the method by name
        var methods = typeof(BLExtensions).GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static)
            .Where(m => m.Name == BlazorLocalizationSymbols.TranslationMethodName)
            .ToList();

        methods.Should().NotBeEmpty(
            $"StringLocalizerExtensions should have a method named '{BlazorLocalizationSymbols.TranslationMethodName}'");
    }

    [Fact]
    public void DisplayMethodName_MatchesExtensionMethod()
    {
        var methods = typeof(BLExtensions).GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static)
            .Where(m => m.Name == BlazorLocalizationSymbols.DisplayMethodName)
            .ToList();

        methods.Should().NotBeEmpty(
            $"StringLocalizerExtensions should have a method named '{BlazorLocalizationSymbols.DisplayMethodName}'");
    }

    [Fact]
    public void KeyParameterName_MatchesTranslationParameter()
    {
        var method = typeof(BLExtensions)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static)
            .First(m => m.Name == BlazorLocalizationSymbols.TranslationMethodName && !m.IsGenericMethod);

        var keyParam = method.GetParameters()
            .FirstOrDefault(p => p.Name == BlazorLocalizationSymbols.KeyParameterName);

        keyParam.Should().NotBeNull(
            $"Translation() should have a parameter named '{BlazorLocalizationSymbols.KeyParameterName}'");
    }

    [Fact]
    public void SourceMessageParameterName_MatchesTranslationParameter()
    {
        var method = typeof(BLExtensions)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static)
            .First(m => m.Name == BlazorLocalizationSymbols.TranslationMethodName && !m.IsGenericMethod);

        var msgParam = method.GetParameters()
            .FirstOrDefault(p => p.Name == BlazorLocalizationSymbols.SourceMessageParameterName);

        msgParam.Should().NotBeNull(
            $"Translation() should have a parameter named '{BlazorLocalizationSymbols.SourceMessageParameterName}'");
    }

    [Fact]
    public void ReplaceWithParameterName_MatchesTranslationParameter()
    {
        var method = typeof(BLExtensions)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static)
            .First(m => m.Name == BlazorLocalizationSymbols.TranslationMethodName && !m.IsGenericMethod);

        var rwParam = method.GetParameters()
            .FirstOrDefault(p => p.Name == BlazorLocalizationSymbols.ReplaceWithParameterName);

        rwParam.Should().NotBeNull(
            $"Translation() should have a parameter named '{BlazorLocalizationSymbols.ReplaceWithParameterName}'");
    }

    // ── Builder type metadata names ────────────────────────────────

    [Fact]
    public void SimpleBuilderMetadataName_MatchesType()
        => BlazorLocalizationSymbols.SimpleBuilderMetadataName
            .Should().Be(typeof(SimpleBuilder).FullName);

    [Fact]
    public void PluralBuilderMetadataName_MatchesType()
        => BlazorLocalizationSymbols.PluralBuilderMetadataName
            .Should().Be(typeof(PluralBuilder).FullName);

    [Fact]
    public void SelectBuilderMetadataName_MatchesType()
        => BlazorLocalizationSymbols.SelectBuilderMetadataName
            .Should().Be(typeof(SelectBuilder<>).FullName);

    [Fact]
    public void SelectPluralBuilderMetadataName_MatchesType()
        => BlazorLocalizationSymbols.SelectPluralBuilderMetadataName
            .Should().Be(typeof(SelectPluralBuilder<>).FullName);
}
