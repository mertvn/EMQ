using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using EMQ.Shared.Core;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;

namespace EMQ.Client.Components;

public partial class MyAutocompleteComponent<TValue> where TValue : notnull
{
    public string SelectedText { get; private set; } = "";

    public TValue? SelectedValue { get; private set; }

    public TValue[] CurrentSearchResults { get; private set; } = Array.Empty<TValue>();

    public bool PreventDefault { get; private set; }

    public bool ShowDropdown { get; private set; }

    public ElementReference InputRef { get; private set; }

    public int CurrentFocus { get; private set; } = -1;

    public bool Answered { get; private set; }

    // todo implement
    [Parameter]
    public int MinLength { get; set; } = 1;

    [Parameter]
    public string? MaxMenuHeight { get; set; }

    [Parameter]
    public string? Placeholder { get; set; }

    [Parameter]
    public bool Disabled { get; set; }

    [Parameter]
    public bool HighlightMatch { get; set; } = true;

    [Parameter]
    public bool RequireConfirmation { get; set; }

    [EditorRequired]
    [Parameter]
    public Func<string, TValue[]> OnSearch { get; set; } = null!;

    [EditorRequired]
    [Parameter]
    public Func<TValue, string> TextField { get; set; } = null!;

    [Parameter]
    public Func<TValue, string>? TextFieldForSelectedText { get; set; }

    [EditorRequired]
    [Parameter]
    public EventCallback<TValue?> OnValueChanged { get; set; }

    [Parameter]
    public string? GuessStr { get; set; }

    public void Close()
    {
        ShowDropdown = false;
        CurrentFocus = -1;
        StateHasChanged();
    }

    public async Task Focus(bool preventScroll = false)
    {
        await InputRef.FocusAsync(preventScroll);
    }

    public async Task Clear(bool raiseValueChanged)
    {
        SelectedText = "";
        SelectedValue = default;
        CurrentFocus = -1;
        CurrentSearchResults = Array.Empty<TValue>();
        Answered = false;
        StateHasChanged();

        if (raiseValueChanged)
        {
            await OnValueChanged.InvokeAsync(SelectedValue);
        }
    }

    public async Task OnSetInputSearch(string value)
    {
        SelectedText = value;
        CurrentSearchResults = OnSearch.Invoke(value);
        CurrentFocus = Math.Clamp(CurrentFocus, -1,
            CurrentSearchResults.Length == 0 ? -1 : CurrentSearchResults.Length - 1);
        ShowDropdown = true;
    }

    private async Task OnKeyDown(KeyboardEventArgs args)
    {
        switch (args.Key)
        {
            case "ArrowUp":
                {
                    PreventDefault = true; // prevent the input box cursor jumping to start
                    ShowDropdown = true;
                    CurrentFocus = Math.Clamp(CurrentFocus - 1, 0,
                        CurrentSearchResults.Length == 0 ? 0 : CurrentSearchResults.Length - 1);
                    await ScrollItemIntoView(CurrentFocus);
                    break;
                }
            case "ArrowDown":
                {
                    PreventDefault = true; // prevent the input box cursor jumping to end
                    ShowDropdown = true;
                    CurrentFocus = Math.Clamp(CurrentFocus + 1, 0,
                        CurrentSearchResults.Length == 0 ? 0 : CurrentSearchResults.Length - 1);
                    await ScrollItemIntoView(CurrentFocus);
                    break;
                }
            case "Enter":
            case "NumpadEnter":
            // case "Tab":
                {
                    await SelectValue();
                    break;
                }
            case "Escape":
                {
                    Close();
                    break;
                }
            default:
                {
                    PreventDefault = false;
                    break;
                }
        }
    }

    private async Task ScrollItemIntoView(int index)
    {
        if (!ShowDropdown || index < 0)
        {
            return;
        }

        string elementId = $"autocomplete-item-{index}"; // todo important this is broken for non-mst
        await _jsRuntime.InvokeVoidAsync("scrollElementIntoView", elementId, true);
    }

    private async Task SelectValue()
    {
        TValue? value = CurrentSearchResults.ElementAtOrDefault(CurrentFocus);
        if (value != null)
        {
            string textField = TextField.Invoke(value);
            string textFieldForSelectedText =
                TextFieldForSelectedText != null ? TextFieldForSelectedText.Invoke(value) : textField;
            bool confirmed = !RequireConfirmation || SelectedText == textFieldForSelectedText;
            SelectedText = textFieldForSelectedText;

            if (confirmed)
            {
                SelectedValue = value;
                CurrentSearchResults = OnSearch.Invoke(SelectedText); // todo? option to not do this
                await OnValueChanged.InvokeAsync(SelectedValue);
                Answered = true;
            }
        }
        else
        {
            SelectedValue = default;
            CurrentSearchResults = OnSearch.Invoke(SelectedText); // todo? option to not do this
            await OnValueChanged.InvokeAsync(SelectedValue);
            Answered = true;
        }

        if (RequireConfirmation)
        {
            ShowDropdown = false;
        }
        else
        {
            Close();
        }
    }

    // todo investigate artist only showing mb names
    private void OnFocus(FocusEventArgs obj)
    {
        ShowDropdown = true;
    }

    private void OnBlur(FocusEventArgs obj)
    {
        Close();
    }

    private async Task Onclick_AutocompleteItem(MouseEventArgs obj, int index)
    {
        CurrentFocus = index;
        await SelectValue();
    }

    private void Onmousedown()
    {
        // Do not remove.
    }
}
