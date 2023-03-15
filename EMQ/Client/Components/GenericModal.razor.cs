using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;

namespace EMQ.Client.Components;

public partial class GenericModal
{
    [Parameter]
    public RenderFragment? ChildContent { get; set; }

    [Parameter]
    public Blazorise.ModalSize Size { get; set; }

    [Parameter]
    public string? Title { get; set; }

    [Parameter]
    public Func<Task>? OkAction { get; set; }

    private Blazorise.Modal? _modalRef;

    public void Show()
    {
        StateHasChanged();
        _modalRef!.Show();
    }

    public void Hide()
    {
        StateHasChanged();
        _modalRef!.Hide();
    }
}
