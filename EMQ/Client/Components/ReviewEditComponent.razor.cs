using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using EMQ.Client.Pages;
using EMQ.Shared.Auth.Entities.Concrete;
using EMQ.Shared.Core;
using EMQ.Shared.Core.SharedDbEntities;
using EMQ.Shared.Library.Entities.Concrete;
using EMQ.Shared.Library.Entities.Concrete.Dto.Request;
using EMQ.Shared.Mod.Entities.Concrete.Dto.Request;
using EMQ.Shared.Quiz.Entities.Abstract;
using EMQ.Shared.Quiz.Entities.Concrete;
using EMQ.Shared.Quiz.Entities.Concrete.Dto.Request;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;

namespace EMQ.Client.Components;

public partial class ReviewEditComponent
{
    [CascadingParameter]
    public EditQueueComponent? EditQueueComponent { get; set; }

    [Parameter]
    public IQueryable<EditQueue>? CurrentEQs { get; set; }

    [Parameter]
    public Func<Task>? ParentStateHasChangedCallback { get; set; }

    [Parameter]
    public bool IsLibraryPage { get; set; }

    private Blazorise.Modal _modalRef = null!;

    public int reviewingId = 1;

    private int oldReviewingId;

    private EditQueue? reviewingItem => CurrentEQs?.FirstOrDefault(x => x.id == reviewingId);

    private bool IsOpen;

    public IEditQueueEntity? Entity { get; set; }

    public IEditQueueEntity? OldEntity { get; set; }

    private bool isReadonly;

    private bool ApplyToNext500Batch { get; set; }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            while (reviewingItem == null)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(50));
            }
        }

        if (oldReviewingId != reviewingId)
        {
            oldReviewingId = reviewingId;
            Entity = null;
            OldEntity = null;

            if (reviewingItem != null)
            {
                switch (reviewingItem.entity_kind)
                {
                    case EntityKind.Song:
                        {
                            Entity = JsonSerializer.Deserialize<Song>(reviewingItem.entity_json)!;
                            if (!string.IsNullOrEmpty(reviewingItem.old_entity_json))
                            {
                                OldEntity = JsonSerializer.Deserialize<Song>(reviewingItem.old_entity_json)!;
                            }

                            isReadonly = CurrentEQs!.Any(x =>
                                x.entity_id == reviewingItem.entity_id && x.id > reviewingItem.id);
                            break;
                        }
                    case EntityKind.SongSource:
                        {
                            Entity = JsonSerializer.Deserialize<SongSource>(reviewingItem.entity_json)!;
                            if (!string.IsNullOrEmpty(reviewingItem.old_entity_json))
                            {
                                OldEntity = JsonSerializer.Deserialize<SongSource>(reviewingItem.old_entity_json)!;
                            }

                            isReadonly = CurrentEQs!.Any(x =>
                                x.entity_id == reviewingItem.entity_id && x.id > reviewingItem.id);
                            break;
                        }
                    case EntityKind.SongArtist:
                        {
                            Entity = JsonSerializer.Deserialize<SongArtist>(reviewingItem.entity_json)!;
                            if (!string.IsNullOrEmpty(reviewingItem.old_entity_json))
                            {
                                OldEntity = JsonSerializer.Deserialize<SongArtist>(reviewingItem.old_entity_json)!;
                            }

                            isReadonly = CurrentEQs!.Any(x =>
                                x.entity_id == reviewingItem.entity_id && x.id > reviewingItem.id);
                            break;
                        }
                    case EntityKind.MergeArtists:
                        {
                            var mergeArtists = JsonSerializer.Deserialize<MergeArtists>(reviewingItem.entity_json)!;
                            HttpResponseMessage res1 = await _client.PostAsJsonAsync("Library/GetSongArtist",
                                new SongArtist { Id = mergeArtists.SourceId });
                            if (res1.IsSuccessStatusCode)
                            {
                                var content = (await res1.Content.ReadFromJsonAsync<ResGetSongArtist>())!;
                                if (content.SongArtists.Any())
                                {
                                    OldEntity = content.SongArtists.First();
                                }
                            }

                            HttpResponseMessage res2 = await _client.PostAsJsonAsync("Library/GetSongArtist",
                                new SongArtist { Id = mergeArtists.Id });
                            if (res2.IsSuccessStatusCode)
                            {
                                var content = (await res2.Content.ReadFromJsonAsync<ResGetSongArtist>())!;
                                if (content.SongArtists.Any())
                                {
                                    Entity = content.SongArtists.First();
                                }
                            }

                            if (Entity == null && OldEntity != null)
                            {
                                Entity = JsonSerializer.Deserialize<SongArtist>(
                                    JsonSerializer.Serialize((SongArtist)OldEntity));
                                OldEntity = null;
                            }

                            isReadonly = false;
                            break;
                        }
                }

                if (isReadonly && reviewingItem.status == ReviewQueueStatus.Pending)
                {
                    if (ClientState.Session != null &&
                        AuthStuff.HasPermission(ClientState.Session, PermissionKind.ReviewEdit))
                    {
                        // todo do this on the server, and in a smarter way
                        await EditQueueComponent!.SendUpdateEditQueueItem(reviewingItem!,
                            "Automatically rejected due to an edit conflict.",
                            ReviewQueueStatus.Rejected);
                    }
                }
            }

            StateHasChanged();
        }
    }

    public async Task Onclick_Reject()
    {
        await EditQueueComponent!.SendUpdateEditQueueItem(reviewingItem!, reviewingItem!.note_mod,
            ReviewQueueStatus.Rejected);
        if (ApplyToNext500Batch)
        {
            ApplyToNext500Batch = false;
            foreach (var eq in CurrentEQs!.Where(x => x.id > reviewingId && x.status == ReviewQueueStatus.Pending)
                         .OrderBy(x => x.id).Take(500))
            {
                await EditQueueComponent!.SendUpdateEditQueueItem(eq, eq.note_mod, ReviewQueueStatus.Rejected);
            }
        }
    }

    public async Task Onclick_Pending()
    {
        await EditQueueComponent!.SendUpdateEditQueueItem(reviewingItem!, reviewingItem!.note_mod,
            ReviewQueueStatus.Pending);
        if (ApplyToNext500Batch)
        {
            ApplyToNext500Batch = false;
            foreach (var eq in CurrentEQs!.Where(x => x.id > reviewingId && x.status == ReviewQueueStatus.Pending)
                         .OrderBy(x => x.id).Take(500))
            {
                await EditQueueComponent!.SendUpdateEditQueueItem(eq, eq.note_mod, ReviewQueueStatus.Pending);
            }
        }
    }

    public async Task Onclick_Approve()
    {
        if (reviewingItem!.entity_kind == EntityKind.MergeArtists)
        {
            bool confirmed = await _jsRuntime.InvokeAsync<bool>("confirm",
                "This action is IRREVERSIBLE. Are you sure you want to continue?");
            if (!confirmed)
            {
                return;
            }
        }

        await EditQueueComponent!.SendUpdateEditQueueItem(reviewingItem!, reviewingItem!.note_mod,
            ReviewQueueStatus.Approved);
        if (ApplyToNext500Batch)
        {
            ApplyToNext500Batch = false;
            foreach (var eq in CurrentEQs!.Where(x => x.id > reviewingId && x.status == ReviewQueueStatus.Pending)
                         .OrderBy(x => x.id).Take(500))
            {
                await EditQueueComponent!.SendUpdateEditQueueItem(eq, eq.note_mod, ReviewQueueStatus.Approved);
            }
        }
    }

    private async Task OnOpened()
    {
        // Console.WriteLine("OnOpened");
        IsOpen = true;
    }

    private async Task OnClosed()
    {
        // Console.WriteLine("OnClosed");
        IsOpen = false;
    }

    public void Show()
    {
        StateHasChanged();
        _modalRef!.Show();
    }
}
