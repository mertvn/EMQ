using System;
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

    public SongReportComponent _songReportComponent { get; set; } = null!;

    private int _currentSongId;

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
}
