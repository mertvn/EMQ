﻿using System;
using System.Collections.Generic;
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

    [Parameter]
    public bool IsEditing { get; set; }

    [Parameter]
    public bool ShowDevelopers { get; set; }

    [Parameter]
    public bool IsQuizPage { get; set; }

    public SongReportComponent _songReportComponent { get; set; } = null!;

    private int _currentSongId;

    private GenericModal _shSongStatsModalRef { get; set; } = null!;

    private Dictionary<GuessKind, IQueryable<SHSongStats>>? SHSongStatsDict { get; set; }

    private bool ForceRender { get; set; }

    private GenericModal _MusicVotesModalRef { get; set; } = null!;

    private IQueryable<MusicVote>? MusicVotes { get; set; }

    public ResGetMusicVotes? ResGetMusicVotes { get; set; }

    protected override bool ShouldRender()
    {
        if (ForceRender || IsEditing)
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

    private async Task DeleteArtist(SongArtist songArtist)
    {
        // todo? generic confirm modal
        bool confirmed = await _jsRuntime.InvokeAsync<bool>("confirm", $"Really delete {songArtist}?");
        if (!confirmed)
        {
            return;
        }

        var res = await _client.PostAsJsonAsync("Mod/DeleteArtist", songArtist.Id);
        if (res.IsSuccessStatusCode)
        {
            // todo remove from (parent) view
            // Song = null;
            // StateHasChanged();
        }
        else
        {
            await _jsRuntime.InvokeVoidAsync("alert", $"Error deleting {songArtist}");
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

        // Unofficial implies NonCanon.
        if (Song.Attributes.HasFlag(SongAttributes.Unofficial))
        {
            Song.Attributes |= SongAttributes.NonCanon;
        }

        await CallStateHasChanged();
    }

    private async Task OnSongTypesCheckboxClick(bool value, SongType type)
    {
        if (value)
        {
            Song!.Type |= type;
        }
        else
        {
            Song!.Type ^= type;
        }

        if (Song.Type < SongType.Standard)
        {
            Song!.Type |= SongType.Standard;
        }
        else if (Song.Type > SongType.Standard)
        {
            Song!.Type &= ~SongType.Standard;
        }

        await CallStateHasChanged();
    }

    private async Task OnclickSongStatsDiv()
    {
        var res = await _client.PostAsJsonAsync("Library/GetSHSongStats", Song!.Id);
        if (res.IsSuccessStatusCode)
        {
            SHSongStatsDict =
                (await res.Content.ReadFromJsonAsync<Dictionary<GuessKind, SHSongStats[]>>())!.ToDictionary(x => x.Key,
                    x => x.Value.AsQueryable());
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

    public async Task CallStateHasChanged()
    {
        ForceRender = true;
        StateHasChanged();
    }
}
