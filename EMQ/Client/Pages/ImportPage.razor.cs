using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using EMQ.Shared.Auth.Entities.Concrete;
using EMQ.Shared.Core;
using EMQ.Shared.Mod.Entities.Concrete.Dto.Request;
using EMQ.Shared.Quiz.Entities.Concrete;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.JSInterop;

namespace EMQ.Client.Pages;

public partial class ImportPage
{
    public List<Song> ImporterPendingSongs { get; set; } = new();

    protected override async Task OnInitializedAsync()
    {
        await _clientUtils.TryRestoreSession();
        if (ClientState.Session == null ||
            !AuthStuff.HasPermission(ClientState.Session.UserRoleKind, PermissionKind.ImportHelper))
        {
            _navigation.NavigateTo("/", true);
            return;
        }

        ImporterPendingSongs = (await _client.GetFromJsonAsync<List<Song>>("Import/GetImporterPendingSongs"))!;
    }

    private async Task Onclick_RunVndbImporter()
    {
        HttpResponseMessage res = await _client.PostAsJsonAsync("Import/RunVndbImporter", "");
        if (res.IsSuccessStatusCode)
        {
            ImporterPendingSongs = (await _client.GetFromJsonAsync<List<Song>>("Import/GetImporterPendingSongs"))!;
        }
    }

    private async Task Onclick_RunMusicBrainzImporter()
    {
        HttpResponseMessage res = await _client.PostAsJsonAsync("Import/RunMusicBrainzImporter", "");
        if (res.IsSuccessStatusCode)
        {
            ImporterPendingSongs = (await _client.GetFromJsonAsync<List<Song>>("Import/GetImporterPendingSongs"))!;
        }
    }

    private async Task OnClick_DownloadPendingSongs()
    {
        string json = JsonSerializer.Serialize(ImporterPendingSongs, Utils.Jso);
        byte[] file = System.Text.Encoding.UTF8.GetBytes(json);
        await _jsRuntime.InvokeVoidAsync("downloadFile", "PendingSongs.json", "application/json", file);
    }


    private async Task InsertSong(Song song)
    {
        var _ = await _client.PostAsJsonAsync("Import/InsertSong", song);
        ImporterPendingSongs = (await _client.GetFromJsonAsync<List<Song>>("Import/GetImporterPendingSongs"))!;
        StateHasChanged();
    }

    private async Task InsertSongBatchMusicBrainzRelease(Song song)
    {
        var allSongsInTheSameMusicBrainzRelease =
            ImporterPendingSongs.Where(x => x.MusicBrainzReleases.Any(y => song.MusicBrainzReleases.Contains(y)))
                .ToList();

        foreach (Song song1 in allSongsInTheSameMusicBrainzRelease)
        {
            _ = await _client.PostAsJsonAsync("Import/InsertSong", song1);
        }

        ImporterPendingSongs = (await _client.GetFromJsonAsync<List<Song>>("Import/GetImporterPendingSongs"))!;
        StateHasChanged();
    }

    private async Task OverwriteMusic(Song newSong)
    {
        string? promptResult = await _jsRuntime.InvokeAsync<string?>("prompt", "Enter music id to overwrite");
        if (int.TryParse(promptResult?.Trim(), out int oldMid))
        {
            var _ = await _client.PostAsJsonAsync("Import/OverwriteMusic", new ReqOverwriteMusic(oldMid, newSong));
            ImporterPendingSongs =
                (await _client.GetFromJsonAsync<List<Song>>("Import/GetImporterPendingSongs"))!;
            StateHasChanged();
        }
    }

    private async Task OnInputFileChange(InputFileChangeEventArgs e)
    {
        Console.WriteLine("OnInputFileChange");
        List<IBrowserFile> files;
        try
        {
            files = e.GetMultipleFiles(1).ToList(); // ToList is required here
        }
        catch (InvalidOperationException)
        {
            return;
        }

        foreach (var file in files)
        {
            using var content = new MultipartFormDataContent();
            var fileContent = new StreamContent(file.OpenReadStream(UploadConstants.MaxFilesizeBytes));
            fileContent.Headers.ContentType = new MediaTypeHeaderValue(file.ContentType);
            content.Add(fileContent, "\"files\"", file.Name);

            var response = await _client.PostAsync("Import/SetPendingSongs", content);
            if (response.IsSuccessStatusCode)
            {
                ImporterPendingSongs = (await _client.GetFromJsonAsync<List<Song>>("Import/GetImporterPendingSongs"))!;
            }
        }
    }
}
