using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using EMQ.Shared.Core;
using EMQ.Shared.Library.Entities.Concrete;
using EMQ.Shared.Library.Entities.Concrete.Dto.Request;
using EMQ.Shared.Mod.Entities.Concrete.Dto.Request;
using EMQ.Shared.Quiz.Entities.Concrete;
using EMQ.Shared.Quiz.Entities.Concrete.Dto.Request;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;

namespace EMQ.Client.Components;

public partial class ReviewComponent
{
    [CascadingParameter]
    public ReviewQueueComponent? ReviewQueueComponent { get; set; }

    [Parameter]
    public IQueryable<RQ>? CurrentRQs { get; set; }

    [Parameter]
    public Func<Task>? ParentStateHasChangedCallback { get; set; }

    [Parameter]
    public bool IsRQComp { get; set; }

    private Blazorise.Modal _modalRef = null!;

    private const string VideoElementId = "videoReview";

    public int reviewingId = 1;

    private RQ? reviewingItem => CurrentRQs?.FirstOrDefault(x => x.id == reviewingId);

    private bool IsOpen;

    private bool controls = true;

    private bool IsReady { get; set; } = true;

    private bool ApplyToBGMBatch { get; set; }

    private RQ[] BGMBatch
    {
        get
        {
            if (CurrentRQs == null || reviewingItem == null ||
                !reviewingItem.Song.Sources.Any(x => x.SongTypes.Contains(SongSourceSongType.BGM)))
            {
                return Array.Empty<RQ>();
            }

            return CurrentRQs.Where(x =>
                x.submitted_by == reviewingItem.submitted_by &&
                ((x.Song.DataSource == DataSourceKind.MusicBrainz &&
                  x.Song.MusicBrainzReleases.Any(y => reviewingItem.Song.MusicBrainzReleases.Contains(y))) ||
                 x.Song.DataSource == DataSourceKind.EMQ && x.Song.IsBGM) &&
                (x.submitted_on - reviewingItem.submitted_on > TimeSpan.FromMinutes(0) &&
                 x.submitted_on - reviewingItem.submitted_on < TimeSpan.FromMinutes(60))).ToArray();
        }
    }

    public async Task Onclick_Reject()
    {
        if (IsReady)
        {
            IsReady = false;
            await ReviewQueueComponent!.SendUpdateReviewQueueItem(reviewingItem!, reviewingItem!.reason,
                ReviewQueueStatus.Rejected);
            if (reviewingItem.is_video)
            {
                var weba = CurrentRQs!.FirstOrDefault(x =>
                    x.music_id == reviewingItem.music_id && x.id > reviewingItem.id &&
                    x.url.Replace("weba/", "").EndsWith($"{reviewingItem.url.Replace(".webm", ".weba")}"));
                if (weba != null)
                {
                    await ReviewQueueComponent!.SendUpdateReviewQueueItem(weba, weba.reason,
                        ReviewQueueStatus.Rejected);
                }
            }

            if (ApplyToBGMBatch && reviewingItem.Song.Sources.Any(x => x.SongTypes.Contains(SongSourceSongType.BGM)))
            {
                foreach (RQ rq in BGMBatch.Reverse())
                {
                    await ReviewQueueComponent!.SendUpdateReviewQueueItem(rq, rq.reason, ReviewQueueStatus.Rejected);
                }
            }

            IsReady = true;
        }
    }

    public async Task Onclick_Pending()
    {
        if (IsReady)
        {
            IsReady = false;
            await ReviewQueueComponent!.SendUpdateReviewQueueItem(reviewingItem!, reviewingItem!.reason,
                ReviewQueueStatus.Pending);
            if (reviewingItem.is_video)
            {
                var weba = CurrentRQs!.FirstOrDefault(x =>
                    x.music_id == reviewingItem.music_id && x.id > reviewingItem.id &&
                    x.url.Replace("weba/", "").EndsWith($"{reviewingItem.url.Replace(".webm", ".weba")}"));
                if (weba != null)
                {
                    await ReviewQueueComponent!.SendUpdateReviewQueueItem(weba, weba.reason, ReviewQueueStatus.Pending);
                }
            }

            if (ApplyToBGMBatch && reviewingItem.Song.Sources.Any(x => x.SongTypes.Contains(SongSourceSongType.BGM)))
            {
                foreach (RQ rq in BGMBatch.Reverse())
                {
                    await ReviewQueueComponent!.SendUpdateReviewQueueItem(rq, rq.reason, ReviewQueueStatus.Pending);
                }
            }

            IsReady = true;
        }
    }

    public async Task Onclick_Approve()
    {
        if (IsReady)
        {
            IsReady = false;
            await ReviewQueueComponent!.SendUpdateReviewQueueItem(reviewingItem!, reviewingItem!.reason,
                ReviewQueueStatus.Approved);
            if (reviewingItem.is_video)
            {
                var weba = CurrentRQs!.FirstOrDefault(x =>
                    x.music_id == reviewingItem.music_id && x.id > reviewingItem.id &&
                    x.url.Replace("weba/", "").EndsWith($"{reviewingItem.url.Replace(".webm", ".weba")}"));
                if (weba != null)
                {
                    await ReviewQueueComponent!.SendUpdateReviewQueueItem(weba, weba.reason,
                        ReviewQueueStatus.Approved);
                }
            }

            if (ApplyToBGMBatch && reviewingItem.Song.Sources.Any(x => x.SongTypes.Contains(SongSourceSongType.BGM)))
            {
                foreach (RQ rq in BGMBatch.Reverse())
                {
                    await ReviewQueueComponent!.SendUpdateReviewQueueItem(rq, rq.reason, ReviewQueueStatus.Approved);
                }
            }

            IsReady = true;
        }
    }

    private async Task OnOpened()
    {
        // Console.WriteLine("OnOpened");
        IsOpen = true;

        await Task.Delay(500);
        await _jsRuntime.InvokeVoidAsync("setVideoVolume", VideoElementId,
            ClientState.Preferences.VolumeMaster / 100f);
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

    // todo? changes made after clicking on analysis in the library will not be reflected in the review queue
    private async Task SendEditSongLinkDetailsReq()
    {
        if (IsReady)
        {
            IsReady = false;
            var req = new ReqEditSongLinkDetails(new SongLink()
            {
                Url = reviewingItem!.url,
                Attributes = reviewingItem.attributes,
                Lineage = reviewingItem.lineage,
                Comment = reviewingItem.comment,
            }, reviewingItem.music_id);
            var res = await _client.PostAsJsonAsync("Library/EditSongLinkDetails", req);
            if (!res.IsSuccessStatusCode)
            {
                await _jsRuntime.InvokeVoidAsync("alert",
                    $"Error: {res.StatusCode:D} {res.StatusCode} {await res.Content.ReadAsStringAsync()}");
            }

            IsReady = true;
        }
    }

    private async Task OnAttributesCheckboxClick(bool value, SongLinkAttributes attribute)
    {
        if (value)
        {
            if (!reviewingItem!.is_video &&
                attribute is SongLinkAttributes.AudioReplaced or SongLinkAttributes.TwoPassEncoding)
            {
                return;
            }

            reviewingItem!.attributes |= attribute;
        }
        else
        {
            reviewingItem!.attributes ^= attribute;
        }

        await SendEditSongLinkDetailsReq();
        StateHasChanged();
        ParentStateHasChangedCallback?.Invoke();
    }

    private async Task OnLineageCheckboxClick(bool value, SongLinkLineage lineage)
    {
        if (value)
        {
            reviewingItem!.lineage |= lineage;
        }
        else
        {
            reviewingItem!.lineage ^= lineage;
        }

        await SendEditSongLinkDetailsReq();
        StateHasChanged();
        ParentStateHasChangedCallback?.Invoke();
    }
}
