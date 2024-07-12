using System;
using System.Globalization;
using System.Linq;
using System.Net.Http.Json;
using System.Threading.Tasks;
using EMQ.Shared.Auth.Entities.Concrete.Dto.Request;
using EMQ.Shared.Auth.Entities.Concrete.Dto.Response;
using EMQ.Shared.Core.SharedDbEntities;
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

    private bool ForceRender { get; set; }

    private GenericModal _MusicVotesModalRef { get; set; } = null!;

    private IQueryable<MusicVote>? MusicVotes { get; set; }

    public ResGetMusicVotes? ResGetMusicVotes { get; set; }

    protected override bool ShouldRender()
    {
        if (ForceRender)
        {
            ForceRender = false;
            return true;
        }

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

    private async Task OnclickSongRatingDiv()
    {
        var res = await _client.PostAsJsonAsync("Auth/GetMusicVotes", Song!.Id);
        if (res.IsSuccessStatusCode)
        {
            ResGetMusicVotes = (await res.Content.ReadFromJsonAsync<ResGetMusicVotes>())!;
            MusicVotes = ResGetMusicVotes.MusicVotes.AsQueryable();
            _MusicVotesModalRef.Show();
        }
    }

    private async Task OnSongVote(string value)
    {
        value = value.Replace(',', '.');
        if (float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out float f))
        {
            short sh = (short)(f * 10);
            if (sh is >= 10 and <= 100)
            {
                var res = await SendUpsertMusicVoteReq(Song!.Id, sh);
                if (res != null)
                {
                    ClientState.MusicVotes[Song.Id] = res;
                }
            }
        }

        ForceRender = true;
    }

    private async Task<MusicVote?> SendUpsertMusicVoteReq(int musicId, short? vote)
    {
        var req = new ReqUpsertMusicVote(musicId, vote);
        var res = await _client.PostAsJsonAsync("Auth/UpsertMusicVote", req);
        return res.IsSuccessStatusCode ? (await res.Content.ReadFromJsonAsync<MusicVote>())! : null;
    }
}
