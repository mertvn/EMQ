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

    private Blazorise.Modal _modalRef = null!;

    private const string VideoElementId = "videoReview";

    private int reviewingId = 1;

    private RQ? reviewingItem => CurrentRQs?.SingleOrDefault(x => x.id == reviewingId);

    private string? videoSrc
    {
        get
        {
            if (reviewingItem is null)
            {
                return null;
            }

            string url = reviewingItem.url;

            // todo
            bool local = false;
            if (local)
            {
                string localPath = $@"emqsongsbackup/{reviewingItem.id}-{reviewingItem.url.LastSegment()}";
                url = localPath;
            }

            return url;
        }
    }

    private bool IsOpen;

    private bool controls = true;

    protected override async Task OnInitializedAsync()
    {
    }

    public async Task Onclick_Reject()
    {
        await ReviewQueueComponent!.SendUpdateReviewQueueItem(reviewingItem!, reviewingItem!.reason,
            ReviewQueueStatus.Rejected);
        if (reviewingItem.is_video)
        {
            var weba = CurrentRQs?.FirstOrDefault(x =>
                x.music_id == reviewingItem.music_id && x.id > reviewingItem.id &&
                x.url.Replace("weba/", "").EndsWith($"{reviewingItem.url.Replace(".webm", ".weba")}"));
            if (weba != null)
            {
                await ReviewQueueComponent!.SendUpdateReviewQueueItem(weba, weba.reason, ReviewQueueStatus.Rejected);
            }
        }
    }

    public async Task Onclick_Pending()
    {
        await ReviewQueueComponent!.SendUpdateReviewQueueItem(reviewingItem!, reviewingItem!.reason,
            ReviewQueueStatus.Pending);
        if (reviewingItem.is_video)
        {
            var weba = CurrentRQs?.FirstOrDefault(x =>
                x.music_id == reviewingItem.music_id && x.id > reviewingItem.id &&
                x.url.Replace("weba/", "").EndsWith($"{reviewingItem.url.Replace(".webm", ".weba")}"));
            if (weba != null)
            {
                await ReviewQueueComponent!.SendUpdateReviewQueueItem(weba, weba.reason, ReviewQueueStatus.Pending);
            }
        }
    }

    public async Task Onclick_Approve()
    {
        await ReviewQueueComponent!.SendUpdateReviewQueueItem(reviewingItem!, reviewingItem!.reason,
            ReviewQueueStatus.Approved);
        if (reviewingItem.is_video)
        {
            var weba = CurrentRQs?.FirstOrDefault(x =>
                x.music_id == reviewingItem.music_id && x.id > reviewingItem.id &&
                x.url.Replace("weba/", "").EndsWith($"{reviewingItem.url.Replace(".webm", ".weba")}"));
            if (weba != null)
            {
                await ReviewQueueComponent!.SendUpdateReviewQueueItem(weba, weba.reason, ReviewQueueStatus.Approved);
            }
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
}
