using BlazorLocalization.Extensions;

namespace SampleBlazorApp.Components.Pages;

public partial class EdgeCasePage
{
    private string? _codeBehindGreeting;

    protected override void OnInitialized()
    {
        _codeBehindGreeting = Loc.Translation("CB.CodeBehind", "From code-behind").ToString();
    }
}
