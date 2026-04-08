namespace BlazorLocalization.Extractor.Domain;

/// <summary>
/// Classifies the linguistic form of a <see cref="TranslationSourceText"/> for display purposes.
/// Presentation-layer helper — not a domain concept.
/// </summary>
public enum TranslationForm
{
    Simple,
    Plural,
    Ordinal,
    Select,
    SelectPlural
}

public static class TranslationFormExtensions
{
    public static TranslationForm? From(TranslationSourceText? sourceText) =>
        sourceText switch
        {
            SingularText => TranslationForm.Simple,
            PluralText p => p.IsOrdinal ? TranslationForm.Ordinal : TranslationForm.Plural,
            SelectText => TranslationForm.Select,
            SelectPluralText => TranslationForm.SelectPlural,
            null => null,
            _ => null
        };
}
