namespace BlazorLocalization.Extensions.Translation;

/// <summary>
/// Cache key suffix constants matching Crowdin's i18next JSON plural and select conventions.
/// </summary>
internal static class KeySuffix
{
    public const string Zero = "_zero";
    public const string One = "_one";
    public const string Two = "_two";
    public const string Few = "_few";
    public const string Many = "_many";
    public const string Other = "_other";

    public static string ForCategory(string category) => $"_{category}";
    public static string ForExactly(int value) => $"_exactly_{value}";
    public static string ForSelect(string selectValue) => $"_{selectValue}";
}
