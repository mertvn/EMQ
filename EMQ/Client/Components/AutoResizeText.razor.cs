using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace EMQ.Client.Components;

public partial class AutoResizeText
{
    [Parameter]
    public string? Text { get; set; }

    [Parameter]
    public string? ContainerSelector { get; set; }

    [Parameter]
    public double MinFontSize { get; set; } = 8;

    [Parameter]
    public double MaxFontSize { get; set; } = 16;

    public readonly string ElementId = $"auto-resize-{Guid.NewGuid():N}";

    private IJSObjectReference? _cleanup;

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            try
            {
                _cleanup = await _jsRuntime.InvokeAsync<IJSObjectReference?>("initAutoResize",
                    ElementId,
                    ContainerSelector,
                    MinFontSize,
                    MaxFontSize);
            }
            catch (JSException)
            {
                // https://github.com/dotnet/aspnetcore/issues/52070
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            if (_cleanup != null)
            {
                await _cleanup.InvokeVoidAsync("apply");
            }
        }
        catch
        {
            // ignored
        }
    }
}
