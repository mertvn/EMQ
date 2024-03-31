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
    [Parameter]
    public List<RQ> CurrentRQs { get; set; } = new();

    [Parameter]
    public Func<Task>? ParentStateHasChangedCallback { get; set; }

    private Blazorise.Modal _modalRef = null!;

    private const string VideoElementId = "videoReview";

    private int reviewingId = 1;

    private RQ? reviewingItem => CurrentRQs.SingleOrDefault(x => x.id == reviewingId);

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
        await SendUpdateReviewQueueItem(reviewingItem!, reviewingItem!.reason, ReviewQueueStatus.Rejected);
        if (reviewingItem.is_video)
        {
            var weba = CurrentRQs.FirstOrDefault(x =>
                x.url.Replace("weba/", "").EndsWith($"{reviewingItem.url.Replace(".webm", ".weba")}"));
            if (weba != null)
            {
                await SendUpdateReviewQueueItem(weba, weba.reason, ReviewQueueStatus.Rejected);
            }
        }
    }

    public async Task Onclick_Pending()
    {
        await SendUpdateReviewQueueItem(reviewingItem!, reviewingItem!.reason, ReviewQueueStatus.Pending);
        if (reviewingItem.is_video)
        {
            var weba = CurrentRQs.FirstOrDefault(x =>
                x.url.Replace("weba/", "").EndsWith($"{reviewingItem.url.Replace(".webm", ".weba")}"));
            if (weba != null)
            {
                await SendUpdateReviewQueueItem(weba, weba.reason, ReviewQueueStatus.Pending);
            }
        }
    }

    public async Task Onclick_Approve()
    {
        await SendUpdateReviewQueueItem(reviewingItem!, reviewingItem!.reason, ReviewQueueStatus.Approved);
        if (reviewingItem.is_video)
        {
            var weba = CurrentRQs.FirstOrDefault(x =>
                x.url.Replace("weba/", "").EndsWith($"{reviewingItem.url.Replace(".webm", ".weba")}"));
            if (weba != null)
            {
                await SendUpdateReviewQueueItem(weba, weba.reason, ReviewQueueStatus.Approved);
            }
        }
    }

    private async Task SendUpdateReviewQueueItem(RQ item, string? reason, ReviewQueueStatus reviewQueueStatus)
    {
        var req = new ReqUpdateReviewQueueItem(item.id, reviewQueueStatus, reason);
        var res = await _client.PostAsJsonAsync("Mod/UpdateReviewQueueItem", req);
        if (res.IsSuccessStatusCode)
        {
            var resFindRQ = await _client.PostAsJsonAsync("Library/FindRQ", item.id);
            if (resFindRQ.IsSuccessStatusCode)
            {
                RQ rq = (await resFindRQ.Content.ReadFromJsonAsync<RQ>())!;
                int index = CurrentRQs.IndexOf(item);
                // Console.WriteLine(index);
                CurrentRQs[index] = rq;

                ParentStateHasChangedCallback?.Invoke(); // update ReviewQueueComponent row
            }
        }

        StateHasChanged();
    }

    private async Task OnOpened()
    {
        // Console.WriteLine("OnOpened");
        IsOpen = true;

        await Task.Delay(300);
        if (ClientState.Session != null)
        {
            await _jsRuntime.InvokeVoidAsync("setVideoVolume", VideoElementId,
                ClientState.Session.Player.Preferences.VolumeMaster / 100f);
        }
    }

    private async Task OnClosed()
    {
        // Console.WriteLine("OnClosed");
        IsOpen = false;
    }
}
