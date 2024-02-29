using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using EMQ.Shared.Auth.Entities.Concrete;
using EMQ.Shared.Library.Entities.Concrete;
using EMQ.Shared.Library.Entities.Concrete.Dto.Request;
using EMQ.Shared.Mod.Entities.Concrete.Dto.Request;
using EMQ.Shared.Quiz.Entities.Concrete;
using Microsoft.JSInterop;

namespace EMQ.Client.Pages;

public partial class ModPage
{
    public List<SongReport> SongReports { get; set; } = new();

    public List<Song> ImporterPendingSongs { get; set; } = new();

    protected override async Task OnInitializedAsync()
    {
        await _clientUtils.TryRestoreSession();
        if (ClientState.Session == null ||
            !AuthStuff.HasPermission(ClientState.Session.UserRoleKind, PermissionKind.Moderator))
        {
            _navigation.NavigateTo("/", true);
            return;
        }

        var req = new ReqFindSongReports(DateTime.UtcNow.AddDays(-30), DateTime.UtcNow);
        var res = await _client.PostAsJsonAsync("Library/FindSongReports", req);
        if (res.IsSuccessStatusCode)
        {
            var songReports = await res.Content.ReadFromJsonAsync<List<SongReport>>();
            foreach (SongReport songReport in songReports!)
            {
                songReport.Song = null!;
            }

            SongReports = songReports;
        }

        if (AuthStuff.HasPermission(ClientState.Session.UserRoleKind, PermissionKind.Admin))
        {
            ImporterPendingSongs = (await _client.GetFromJsonAsync<List<Song>>("Mod/GetImporterPendingSongs"))!;
        }
    }

    private async Task Onclick_RunGc()
    {
        HttpResponseMessage res = await _client.PostAsJsonAsync("Mod/RunGc", "");
        if (res.IsSuccessStatusCode)
        {
        }
    }

    private async Task Onclick_RunAnalysis()
    {
        HttpResponseMessage res = await _client.PostAsJsonAsync("Mod/RunAnalysis", "");
        if (res.IsSuccessStatusCode)
        {
        }
    }

    private async Task Onclick_RunVndbImporter()
    {
        HttpResponseMessage res = await _client.PostAsJsonAsync("Mod/RunVndbImporter", "");
        if (res.IsSuccessStatusCode)
        {
            ImporterPendingSongs = (await _client.GetFromJsonAsync<List<Song>>("Mod/GetImporterPendingSongs"))!;
        }
    }

    private async Task Onclick_RunMusicBrainzImporter()
    {
        HttpResponseMessage res = await _client.PostAsJsonAsync("Mod/RunMusicBrainzImporter", "");
        if (res.IsSuccessStatusCode)
        {
            ImporterPendingSongs = (await _client.GetFromJsonAsync<List<Song>>("Mod/GetImporterPendingSongs"))!;
        }
    }

    private async Task OnClick_DownloadSongLite()
    {
        string res = await _client.GetStringAsync("Mod/ExportSongLite");
        byte[] file = System.Text.Encoding.UTF8.GetBytes(res);
        await _jsRuntime.InvokeVoidAsync("downloadFile", "SongLite.json", "application/json", file);
    }

    private async Task OnClick_DownloadSongLite_MB()
    {
        string res = await _client.GetStringAsync("Mod/ExportSongLite_MB");
        byte[] file = System.Text.Encoding.UTF8.GetBytes(res);
        await _jsRuntime.InvokeVoidAsync("downloadFile", "SongLite_MB.json", "application/json", file);
    }

    private async Task Onclick_ToggleIsServerReadOnly()
    {
        HttpResponseMessage res = await _client.PostAsJsonAsync("Mod/ToggleIsServerReadOnly", "");
        if (res.IsSuccessStatusCode)
        {
        }
    }

    private async Task Onclick_ToggleIsSubmissionDisabled()
    {
        HttpResponseMessage res = await _client.PostAsJsonAsync("Mod/ToggleIsSubmissionDisabled", "");
        if (res.IsSuccessStatusCode)
        {
        }
    }

    private async Task InsertSong(Song song)
    {
        var res = await _client.PostAsJsonAsync("Mod/InsertSong", song);
        if (res.IsSuccessStatusCode)
        {
            ImporterPendingSongs = (await _client.GetFromJsonAsync<List<Song>>("Mod/GetImporterPendingSongs"))!;
            StateHasChanged();
        }
    }

    private async Task InsertSongBatchMusicBrainzRelease(Song song)
    {
        var allSongsInTheSameMusicBrainzRelease =
            ImporterPendingSongs.Where(x => x.MusicBrainzReleases.Any(y => song.MusicBrainzReleases.Contains(y)))
                .ToList();

        foreach (Song song1 in allSongsInTheSameMusicBrainzRelease)
        {
            _ = await _client.PostAsJsonAsync("Mod/InsertSong", song1);
        }

        ImporterPendingSongs = (await _client.GetFromJsonAsync<List<Song>>("Mod/GetImporterPendingSongs"))!;
        StateHasChanged();
    }

    private async Task OverwriteMusic(Song newSong)
    {
        string? promptResult = await _jsRuntime.InvokeAsync<string?>("prompt", "Enter music id to overwrite");
        if (int.TryParse(promptResult?.Trim(), out int oldMid))
        {
            var res = await _client.PostAsJsonAsync("Mod/OverwriteMusic", new ReqOverwriteMusic(oldMid, newSong));
            if (res.IsSuccessStatusCode)
            {
                ImporterPendingSongs =
                    (await _client.GetFromJsonAsync<List<Song>>("Mod/GetImporterPendingSongs"))!;
                StateHasChanged();
            }
        }
    }
}
