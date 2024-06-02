using System;
using System.Linq;
using System.Net.Http.Json;
using System.Threading.Tasks;
using EMQ.Shared.Quiz.Entities.Concrete;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace EMQ.Client.Components;

public partial class SongInfoCardComponent
{
    [Parameter]
    public Song? Song { get; set; }

    [Parameter]
    public bool IsModPage { get; set; }

    public SongReportComponent _songReportComponent { get; set; } = null!;

    private int _currentSongId;

    private GenericModal _shSongStatsModalRef { get; set; } = null!;

    private IQueryable<SHSongStats>? SHSongStats { get; set; }

    protected override bool ShouldRender()
    {
        if (Song is null || _currentSongId == Song.Id)
        {
            // Console.WriteLine("should not render");
            return false;
        }
        else
        {
            // Console.WriteLine("should render");
            _currentSongId = Song.Id;
            return true;
        }
    }

    private async Task DeleteSong(Song song)
    {
        // todo? generic confirm modal
        bool confirmed = await _jsRuntime.InvokeAsync<bool>("confirm", $"Really delete {song}?");
        if (!confirmed)
        {
            return;
        }

        var res = await _client.PostAsJsonAsync("Mod/DeleteSong", song.Id);
        if (res.IsSuccessStatusCode)
        {
            // todo remove from (parent) view
            // Song = null;
            // StateHasChanged();
        }
        else
        {
            await _jsRuntime.InvokeVoidAsync("alert", $"Error deleting {song}");
        }
    }

    private async Task OnSongAttributesCheckboxClick(bool value, SongAttributes attribute)
    {
        if (value)
        {
            Song!.Attributes |= attribute;
        }
        else
        {
            Song!.Attributes ^= attribute;
        }

        await SetSongAttributes(Song);
    }

    private async Task SetSongAttributes(Song song)
    {
        var res = await _client.PostAsJsonAsync("Mod/SetSongAttributes", song);
        if (!res.IsSuccessStatusCode)
        {
            await _jsRuntime.InvokeVoidAsync("alert", $"Error setting attributes for {song}");
        }
    }

    private async Task OnclickSongStatsDiv()
    {
        var res = await _client.PostAsJsonAsync("Library/GetSHSongStats", Song!.Id);
        if (res.IsSuccessStatusCode)
        {
            SHSongStats = (await res.Content.ReadFromJsonAsync<SHSongStats[]>())!.AsQueryable();
            _shSongStatsModalRef.Show();
        }
    }
}
