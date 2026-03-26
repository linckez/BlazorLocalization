[< Back to README](../README.md)

# Examples

All examples assume an injected localizer:

```razor
@inject IStringLocalizer<Home> Loc
```

---

## Simple

```razor
<h1>@Loc.Translation("Home.Title", "Welcome to our app")</h1>
```

If translations haven't loaded yet, your source text is shown — users never see blank strings or raw keys.

## Placeholders

Named placeholders are resolved by [SmartFormat](https://github.com/axuno/SmartFormat). Pass any object — properties become placeholders.

```razor
<p>@Loc.Translation("Home.Greeting", "Hello, {Name}!", new { Name = user.Name })</p>

<p>@Loc.Translation("Home.Stats", "Showing {Count} of {Total} items", new { Count = 5, Total = 100 })</p>
```

## Plurals

Chain `.One()`, `.Other()`, and any other [CLDR plural category](https://www.unicode.org/cldr/charts/46/supplemental/language_plural_rules.html) your target languages need. The correct form is chosen automatically based on the current culture.

```razor
<p>@(Loc.Translation("Cart.Items", howMany: cartCount, replaceWith: new { ItemCount = cartCount })
    .One("1 item in your cart")
    .Other("{ItemCount} items in your cart"))</p>
```

`howMany` determines which form to pick. Pass it in `replaceWith` too if the message needs to display it.

Most languages only need `.One()` and `.Other()`. Some need more:

```razor
@* Arabic uses all six CLDR categories *@
<p>@(Loc.Translation("Items", howMany: itemCount, replaceWith: new { ItemCount = itemCount })
    .Zero("لا عناصر")
    .One("عنصر واحد")
    .Two("عنصران")
    .Few("{ItemCount} عناصر")
    .Many("{ItemCount} عنصرًا")
    .Other("{ItemCount} عنصر"))</p>
```

**Corresponding translation files:**

```json
{
  "Cart.Items_one": "1 vare i din kurv",
  "Cart.Items_other": "{ItemCount} varer i din kurv"
}
```

## Ordinals

Ordinal ranking (1st, 2nd, 3rd, …). Chain `.Ordinal()` before the category methods.

```razor
<p>@(Loc.Translation("Race.Place", howMany: position, replaceWith: new { Position = position })
    .Ordinal()
    .One("{Position}st place")
    .Two("{Position}nd place")
    .Few("{Position}rd place")
    .Other("{Position}th place"))</p>
```

The categories above are for English. Other languages define their own ordinal rules in CLDR — for example, Swedish uses only `Other` for all ordinals. The correct form is chosen automatically based on the current culture.

## Exact Counts

Override a specific count with a precise message. Checked before CLDR category rules.

```razor
<p>@(Loc.Translation("Cart.Items", howMany: cartCount)
    .Exactly(0, "Your cart is empty")
    .One("1 item in your cart")
    .Other("Several items in your cart"))</p>
```

## Select

Branch on a categorical enum value — gender, role, formality, or any domain concept.

```razor
<p>@(Loc.Translation("Greeting", select: userTier)
    .When(Tier.Premium, "Welcome back, VIP!")
    .Otherwise("Welcome!"))</p>
```

## Select + Plural

Combine categorical branching with plural forms.

```razor
<p>@(Loc.Translation("Inbox", select: Gender.Female, howMany: msgCount, replaceWith: new { MessageCount = msgCount })
    .When(Gender.Female)
    .One("She has {MessageCount} message")
    .Other("She has {MessageCount} messages")
    .Otherwise()
    .One("They have {MessageCount} message")
    .Other("They have {MessageCount} messages"))</p>
```

## Inline Translations

Already know the translation? Write it where you have the context — no need to switch to Crowdin and back:

```razor
<h1>@(Loc.Translation("Home.Title", "Welcome!")
    .For("da", "Velkommen!")
    .For("es", "¡Bienvenido!"))</h1>
```

Works on all builder types. Here's a plural with inline Danish translations:

```razor
<p>@(Loc.Translation("Cart.Items", howMany: itemCount)
    .One("1 item").Other("Several items")
    .For("da")
    .One("1 vare").Other("Flere varer"))</p>
```

The translation provider always wins when a translation exists. Inline per-locale source texts serve as a starting point for translators and a fallback when the provider hasn't delivered yet.

## Enums

Mark enum members with `[Translation]`, resolve with `Display()`.

```csharp
public enum FlightStatus
{
    [Translation("Delayed")]
    [Translation("Forsinket", Locale = "da")]
    [Translation("Retrasado", Locale = "es-MX")]
    Delayed,

    [Translation("On time")]
    OnTime
}
```

```razor
<p>@Loc.Display(FlightStatus.Delayed)</p>
```

The auto-generated key is `Enum.{TypeName}_{MemberName}` (e.g. `Enum.FlightStatus_Delayed`).

Same fallback as `Translation()`: translation provider → inline per-locale source text → source text → member name.

Inline per-locale source texts work the same way as `.For()` — write them at the declaration site, the provider wins when a real translation exists.

Override the auto-generated key with `Key`:

```csharp
[Translation("Arrived a bit late", Key = "Flight.Late")]
ArrivedABitLate
```

---

## Standard IStringLocalizer

Microsoft's built-in indexer and `GetString()` still work — this is the standard `IStringLocalizer` API that every ASP.NET Core developer already knows:

```razor
<h1>@Loc["Home.Title"]</h1>
<p>@Loc.GetString("Home.Title")</p>
```

If translations haven't loaded yet, the raw key is returned (`"Home.Title"`). Prefer `Translation()` — it gives users readable source text instead of a cryptic key.
