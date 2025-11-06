using System;
using Microsoft.AspNetCore.Components.Web;

namespace EMQ.Client;

public class GlobalKeypressService
{
    public event Action<KeyboardEventArgs>? OnKeypress;

    public void NotifyKeypress(KeyboardEventArgs args)
    {
        OnKeypress?.Invoke(args);
    }
}
