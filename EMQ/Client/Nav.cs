using Microsoft.AspNetCore.Components;

namespace EMQ.Client;

public class Nav : NavigationManager
{
    public Nav(string baseUri)
    {
        _baseUri = baseUri;
        Initialize(_baseUri, _baseUri);
    }

    private string _baseUri { get; }

    protected override void NavigateToCore(string uri, bool forceLoad)
    {
        NotifyLocationChanged(false);
    }
}
