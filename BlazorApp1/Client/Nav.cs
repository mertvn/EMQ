using Microsoft.AspNetCore.Components;

namespace BlazorApp1.Client;

public class Nav : NavigationManager
{
    public Nav()
    {
        Initialize("https://localhost:7021/", "https://localhost:7021/");
    }

    protected override void NavigateToCore(string uri, bool forceLoad)
    {
        NotifyLocationChanged(false);
    }
}
