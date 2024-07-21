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
    public string CurrentText { get; set; } = "";

    public TValue? CurrentValue { get; set; }

    public TValue[] CurrentSearchResults = Array.Empty<TValue>();

    public bool PreventDefault { get; set; }

    public bool ShowDropdown { get; set; }

    public ElementReference InputRef { get; set; }

    public int CurrentFocus { get; set; } = -1;

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

    [EditorRequired]
    [Parameter]
    public Func<string, TValue[]> OnSearch { get; set; } = null!;

    [EditorRequired]
    [Parameter]
    public Func<TValue, string> TextField { get; set; } = null!;

    public void Close()
    {
        ShowDropdown = false;
        CurrentFocus = -1;
        StateHasChanged();
    }

    public async Task Focus(bool preventScroll)
    {
        await InputRef.FocusAsync(preventScroll);
    }

    public void Clear()
    {
        CurrentText = "";
        CurrentValue = default;
        CurrentFocus = -1;
        CurrentSearchResults = Array.Empty<TValue>();
        StateHasChanged();
    }

    private async Task OnSetInputSearch(string value)
    {
        CurrentText = value;
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
            case "Tab":
                {
                    SelectValue();
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

        string elementId = $"autocomplete-item-{index}";
        await _jsRuntime.InvokeVoidAsync("scrollElementIntoView", elementId, true);
    }

    private void SelectValue()
    {
        TValue? value = CurrentSearchResults.ElementAtOrDefault(CurrentFocus);
        if (value != null)
        {
            string textField = TextField.Invoke(value);
            CurrentText = textField;
            CurrentValue = value;
            ShowDropdown = false;
            CurrentSearchResults = OnSearch.Invoke(textField);
        }
    }

    private void OnFocus(FocusEventArgs obj)
    {
        ShowDropdown = true;
    }

    private void OnBlur(FocusEventArgs obj)
    {
        Close();
    }

    private async void Onclick_AutocompleteItem(MouseEventArgs obj, int index)
    {
        CurrentFocus = index;
        SelectValue();
    }
}
