using System.Reflection;
using BlazorLocalization.Extensions.Translation;
using Microsoft.Extensions.Localization;

namespace BlazorLocalization.Extensions;

/// <summary>
/// Fluent translation API for application code.
/// Provides source-text fallback, SmartFormat named placeholders, plural forms,
/// enum-based select branching, and inline per-locale source texts on top of <see cref="IStringLocalizer"/>.
/// </summary>
/// <remarks>
/// These methods work with any <see cref="IStringLocalizer"/> implementation, not just
/// <see cref="ProviderBasedStringLocalizer"/>. The key design: callers supply the original text
/// inline — shown immediately when no translation is available rather than displaying a raw key.
/// </remarks>
public static class StringLocalizerExtensions
{
    /// <param name="localizer">The localizer to look up translation keys in.</param>
    extension(IStringLocalizer localizer)
    {
        /// <summary>
        /// Translates a message. Chain <see cref="SimpleBuilder.For"/> to add
        /// per-locale source texts directly in code.
        /// <code>
        /// Loc.Translation(key: "Home.Title", message: "Welcome to our app")
        /// </code>
        /// </summary>
        /// <param name="key">A unique identifier for this translation, e.g. <c>"Home.Title"</c>.</param>
        /// <param name="message">
        /// The original text. Used as fallback when your translation providers
        /// don't have a translation for the user's language.
        /// </param>
        /// <param name="replaceWith">
        /// Optional named values that fill <c>{placeholders}</c> in <paramref name="message"/>,
        /// e.g. <c>new { Name = user.FirstName }</c> fills <c>{Name}</c>.
        /// </param>
        public SimpleBuilder Translation(string key, string message, object? replaceWith = null)
            => new(localizer, key, message, replaceWith);

        /// <summary>
        /// Translates a message with plural forms — different wording depending on quantity.
        /// Chain <c>.One()</c>, <c>.Other()</c>, etc. to define each form:
        /// <code>
        /// Loc.Translation(key: "Cart.Items", howMany: itemCount, replaceWith: new { ItemCount = cart.Items.Count })
        ///     .One(message: "1 item in your cart")
        ///     .Other(message: "{ItemCount} items in your cart")
        /// </code>
        /// Which forms are needed depends on the user's language — some languages
        /// have up to six (see <c>.Zero()</c>, <c>.Two()</c>, <c>.Few()</c>, <c>.Many()</c>).
        /// The correct form is selected automatically based on the current culture.
        /// </summary>
        /// <param name="key">A unique identifier for this translation, e.g. <c>"Cart.Items"</c>.</param>
        /// <param name="howMany">
        /// The quantity that determines which plural form to use
        /// (e.g. 1 → singular, 5 → plural). Only used for selection — pass it
        /// in <paramref name="replaceWith"/> too if the message needs to display it.
        /// </param>
        /// <param name="ordinal">
        /// Pass <c>true</c> to use ordinal rules (1st, 2nd, 3rd) instead of
        /// cardinal (1, 2, 3). Defaults to <c>false</c>.
        /// </param>
        /// <param name="replaceWith">
        /// Optional named values that fill <c>{placeholders}</c> in the message,
        /// e.g. <c>new { ItemCount = cart.Items.Count }</c> fills <c>{ItemCount}</c>.
        /// </param>
        public PluralBuilder Translation(string key, int howMany, bool ordinal = false, object? replaceWith = null)
            => new(localizer, key, howMany, ordinal, replaceWith);

        /// <summary>
        /// Translates a message that varies by an enum value — for example, showing
        /// different wording for each gender, user role, or membership level.
        /// Chain <c>.When()</c> for each case and <c>.Otherwise()</c> for the default:
        /// <code>
        /// Loc.Translation(key: "Greeting", select: userTier)
        ///     .When(select: Tier.Premium, message: "Welcome back, VIP!")
        ///     .Otherwise(message: "Welcome!")
        /// </code>
        /// </summary>
        /// <param name="key">A unique identifier for this translation, e.g. <c>"Greeting"</c>.</param>
        /// <param name="select">The enum value that picks which variant to show.</param>
        /// <param name="replaceWith">
        /// Optional named values that fill <c>{placeholders}</c> in the message,
        /// e.g. <c>new { Name = user.FirstName }</c> fills <c>{Name}</c>.
        /// </param>
        /// <typeparam name="TSelect">An enum type whose members represent the variants.</typeparam>
        public SelectBuilder<TSelect> Translation<TSelect>(string key, TSelect select, object? replaceWith = null)
            where TSelect : Enum
            => new(localizer, key, select, replaceWith);

        /// <summary>
        /// Translates a message that varies by both an enum value and a quantity:
        /// <code>
        /// Loc.Translation(key: "Inbox", select: Gender.Female, howMany: msgCount, replaceWith: new { MessageCount = msgCount })
        ///     .When(select: Gender.Female)
        ///         .One(message: "She has {MessageCount} message")
        ///         .Other(message: "She has {MessageCount} messages")
        ///     .Otherwise()
        ///         .One(message: "They have {MessageCount} message")
        ///         .Other(message: "They have {MessageCount} messages")
        /// </code>
        /// </summary>
        /// <param name="key">A unique identifier for this translation.</param>
        /// <param name="select">The enum value that picks which variant to show.</param>
        /// <param name="howMany">
        /// The quantity that determines which plural form to use
        /// (e.g. 1 → singular, 5 → plural). Only used for selection — pass it
        /// in <paramref name="replaceWith"/> too if the message needs to display it.
        /// </param>
        /// <param name="ordinal">
        /// Pass <c>true</c> to use ordinal rules (1st, 2nd, 3rd) instead of
        /// cardinal (1, 2, 3). Defaults to <c>false</c>.
        /// </param>
        /// <param name="replaceWith">
        /// Optional named values that fill <c>{placeholders}</c> in the message,
        /// e.g. <c>new { MessageCount = msgCount }</c> fills <c>{MessageCount}</c>.
        /// </param>
        /// <typeparam name="TSelect">An enum type whose members represent the variants.</typeparam>
        public SelectPluralBuilder<TSelect> Translation<TSelect>(string key, TSelect select, int howMany, bool ordinal = false, object? replaceWith = null)
            where TSelect : Enum
            => new(localizer, key, select, howMany, ordinal, replaceWith);

        /// <summary>
        /// Returns the localized display text for an enum member decorated with
        /// <see cref="TranslationAttribute"/>.
        /// Resolution chain: provider/cache → <c>[Translation(Locale)]</c> locale-specific text →
        /// <c>[Translation]</c> source text → <c>enum.ToString()</c>.
        /// <code>
        /// Loc.Display(FlightStatus.Delayed)   // → "Forsinket" when culture is da
        /// </code>
        /// </summary>
        /// <param name="value">The enum value to translate.</param>
        /// <typeparam name="TEnum">The enum type. Members should be decorated with <see cref="TranslationAttribute"/>.</typeparam>
        public string Display<TEnum>(TEnum value) where TEnum : struct, Enum
        {
            var enumType = typeof(TEnum);
            var memberName = value.ToString();
            var field = enumType.GetField(memberName, BindingFlags.Public | BindingFlags.Static);
            var attrs = field?.GetCustomAttributes<TranslationAttribute>();

            string? customKey = null;
            Dictionary<string, string>? localeTexts = null;
            string? sourceText = null;

            if (attrs is not null)
            {
                foreach (var attr in attrs)
                {
                    if (attr.Locale is not null)
                    {
                        localeTexts ??= new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                        localeTexts[attr.Locale] = attr.Message;
                    }
                    else
                    {
                        sourceText = attr.Message;
                        customKey ??= attr.Key;
                    }
                }
            }

            var key = customKey ?? $"Enum.{enumType.Name}_{memberName}";

            return TranslationResolver.TryLocalizer(localizer, key)
                ?? TranslationResolver.TryInline(localeTexts)
                ?? sourceText
                ?? memberName;
        }
    }
}
