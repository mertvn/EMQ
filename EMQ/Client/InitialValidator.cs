using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;

namespace EMQ.Client;

public class InitialValidator : ComponentBase
{
    // Get the EditContext from the parent component (EditForm)
    [CascadingParameter]
    private EditContext? CurrentEditContext { get; set; }

    protected override void OnParametersSet()
    {
        CurrentEditContext?.Validate();
    }
}
