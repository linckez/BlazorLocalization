using BlazorLocalization.Extensions;

namespace MudBlazorServerSample;

public enum FlightStatus
{
    [Translation("On time")]
    [Translation("Pünktlich", Locale = "de")]
    [Translation("Na czas", Locale = "pl")]
    [Translation("Til tiden", Locale = "da")]
    OnTime,

    [Translation("Delayed")]
    [Translation("Verspätet", Locale = "de")]
    [Translation("Opóźniony", Locale = "pl")]
    [Translation("Forsinket", Locale = "da")]
    Delayed,

    [Translation("Cancelled")]
    [Translation("Gestrichen", Locale = "de")]
    [Translation("Odwołany", Locale = "pl")]
    [Translation("Aflyst", Locale = "da")]
    Cancelled,
}
