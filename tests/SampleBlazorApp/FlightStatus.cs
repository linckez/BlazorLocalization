using BlazorLocalization.Extensions;

namespace SampleBlazorApp;

public enum FlightStatus
{
    [Translation("Delayed")]
    [Translation("Forsinket", Locale = "da")]
    [Translation("Retrasado", Locale = "es-MX")]
    Delayed,

    [Translation("Arrived a bit late", Key = "Flight.Late")]
    ArrivedABitLate
}
