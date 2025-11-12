using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Net.Http.Json;
using System.Threading.Tasks;
using EMQ.Client.Pages;
using EMQ.Shared.Auth.Entities.Concrete.Dto.Request;
using EMQ.Shared.Core;
using EMQ.Shared.Core.SharedDbEntities;
using EMQ.Shared.Library.Entities.Concrete.Dto.Request;
using EMQ.Shared.Mod.Entities.Concrete.Dto.Request;
using EMQ.Shared.Quiz.Entities.Concrete;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;

namespace EMQ.Client.Components;

public partial class SongInfoCardWrapperComponent
{
    [Parameter]
    public IEnumerable<Song> CurrentSongs { get; set; } = new List<Song>();

    [Parameter]
    public string NoSongsText { get; set; } = "";

    [Parameter]
    public bool IsLibraryPage { get; set; }

    [Parameter]
    public Dictionary<int, Func<Task>>? BatchUploaderCallbacks { get; set; }

    private int VisibleSongsCount => CurrentSongs.Count();

    private string _batchSetSubmittedByText = "";

    private IQueryable<RQ>? CurrentRQs { get; set; }

    private IQueryable<EditQueue>? CurrentEQs { get; set; }

    // public EditSongComponent editSongModalRef { get; set; } = null!;

    private ReviewComponent _reviewComponent = null!;

    private ReviewEditComponent _reviewEditComponent = null!;

    private MusicCommentComponent _musicCommentComponent = null!;

    private MusicCollectionComponent _musicCollectionComponent = null!;

    private Song CurrentSong { get; set; } = new();

    private async Task BatchSetSubmittedBy()
    {
        var links = CurrentSongs.SelectMany(x => x.Links.Where(y => y.IsFileLink && y.SubmittedBy == "[unknown]"));
        string[] urls = links.Select(x => x.Url).ToArray();

        var req = new ReqSetSubmittedBy(urls, _batchSetSubmittedByText);
        var res = await _client.PostAsJsonAsync("Mod/SetSubmittedBy", req);
        if (!res.IsSuccessStatusCode)
        {
            throw new Exception();
        }
    }

    private async Task DeleteSongLink(int mId, string url)
    {
        // todo? generic confirm modal
        bool confirmed = await _jsRuntime.InvokeAsync<bool>("confirm", $"Really delete {url}?");
        if (!confirmed)
        {
            return;
        }

        var req = new ReqDeleteSongLink(mId, url);
        var res = await _client.PostAsJsonAsync("Mod/DeleteSongLink", req);
        if (!res.IsSuccessStatusCode)
        {
            throw new Exception();
        }

        int rowsDeleted = await res.Content.ReadFromJsonAsync<int>();
        if (rowsDeleted > 0)
        {
            var song = CurrentSongs.Single(x => x.Id == mId);
            song.Links.RemoveAll(x => x.Url == url);
        }
        else
        {
            await _jsRuntime.InvokeVoidAsync("alert", $"Error deleting {url}");
        }
    }

    private async Task CallStateHasChanged()
    {
        StateHasChanged();
    }

    private async Task OnclickAnalysis(int mId, SongLink soundLink)
    {
        CurrentRQs = new List<RQ>
        {
            new()
            {
                id = 1,
                music_id = mId,
                url = soundLink.Url,
                type = soundLink.Type,
                is_video = soundLink.IsVideo,
                submitted_by = soundLink.SubmittedBy ?? "",
                submitted_on = default,
                status = ReviewQueueStatus.Pending,
                reason = null,
                analysis = string.Join(", ", soundLink.AnalysisRaw!.Warnings.Select(x => x.ToString())),
                Song = new Song(),
                duration = soundLink.Duration,
                analysis_raw = soundLink.AnalysisRaw,
                sha256 = soundLink.Sha256,
                attributes = soundLink.Attributes,
                lineage = soundLink.Lineage,
                comment = soundLink.Comment,
            }
        }.AsQueryable();

        _reviewComponent.Show();
    }

    private async Task OnclickSongComments(Song song)
    {
        CurrentSong = song;
        _musicCommentComponent.Show();
    }

    private async Task OnclickSongCollections(Song song)
    {
        CurrentSong = song;
        _musicCollectionComponent.Show();
    }

    private async Task SendModifyCollectionEntityReq(int collectionId, int entityId, bool isAdded)
    {
        var req = new ReqModifyCollectionEntity(collectionId, entityId, isAdded);
        var res = await _client.PostAsJsonAsync("Library/ModifyCollectionEntity", req);
        if (res.IsSuccessStatusCode)
        {
            var collection =
                ClientState.ResGetCollectionContainers.CollectionContainers.First(x => x.Collection.id == collectionId);
            if (isAdded)
            {
                collection.CollectionEntities.Add(new CollectionEntity()
                {
                    collection_id = collectionId,
                    entity_id = entityId,
                    modified_at = DateTime.UtcNow,
                    modified_by = ClientState.Session!.Player.Id
                });
            }
            else
            {
                collection.CollectionEntities.RemoveAll(x => x.entity_id == entityId);
            }
        }
    }
}
