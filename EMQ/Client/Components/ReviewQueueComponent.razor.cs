using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Blazorise.DataGrid;
using EMQ.Shared.Library.Entities.Concrete.Dto;
using EMQ.Shared.Library.Entities.Concrete.Dto.Request;
using EMQ.Shared.Mod.Entities.Concrete.Dto.Request;
using EMQ.Shared.Quiz.Entities.Concrete;
using Microsoft.AspNetCore.Components.QuickGrid;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;

namespace EMQ.Client.Components;

public partial class ReviewQueueComponent
{
    private ReviewComponent _reviewComponentRef = null!;

    private readonly PaginationState _pagination = new() { ItemsPerPage = 250 };

    public IQueryable<RQ>? CurrentRQs { get; set; }

    public static Dictionary<int, int> CurrentPendingRQsMIds { get; set; } = new();

    public string CellStyleInlineBlock { get; set; } =
        "max-width: 220px; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; display: inline-block;";

    public string CellStyleInlineBlockShort { get; set; } =
        "max-width: 140px; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; display: inline-block;";

    public DateTime StartDateFilter { get; set; } = DateTime.UtcNow.AddDays(-3);

    public DateTime EndDateFilter { get; set; } = DateTime.UtcNow.AddDays(1);

    public SongSourceSongTypeMode SSSTMFilter { get; set; } = SongSourceSongTypeMode.All;

    private string SubmittedByFilter { get; set; } = "";

    private IQueryable<RQ>? FilteredRQs
    {
        get
        {
            SubmittedByFilter = SubmittedByFilter.Trim();
            var result = CurrentRQs?.AsQueryable(); // deep-copy
            if (!string.IsNullOrWhiteSpace(SubmittedByFilter))
            {
                result = result?.Where(x =>
                    x.submitted_by.Contains(SubmittedByFilter, StringComparison.OrdinalIgnoreCase));
            }

            return result;
        }
    }

    protected override async Task OnInitializedAsync()
    {
        await RefreshRQs();
        var res = await _client.PostAsJsonAsync("Library/FindQueueItemsWithPendingChanges", "");
        if (res.IsSuccessStatusCode)
        {
            var content = await res.Content.ReadFromJsonAsync<ResFindQueueItemsWithPendingChanges>();
            CurrentPendingRQsMIds = content!.RQs;
        }
    }

    public async Task RefreshRQs()
    {
        var req = new ReqFindRQs(StartDateFilter, EndDateFilter, SSSTMFilter, true);
        var res = await _client.PostAsJsonAsync("Library/FindRQs", req);
        if (res.IsSuccessStatusCode)
        {
            var content = await res.Content.ReadFromJsonAsync<List<RQ>>();
            if (content is not null)
            {
                content.Reverse();
                CurrentRQs = content.AsQueryable();
            }
            else
            {
                Console.WriteLine("Failed to find RQs");
            }
        }

        StateHasChanged();
    }

    private async Task CallStateHasChanged()
    {
        StateHasChanged();
    }

    private async Task DeleteReviewQueueItem(int rqId)
    {
        // todo? generic confirm modal
        bool confirmed = await _jsRuntime.InvokeAsync<bool>("confirm", $"Really delete {rqId}?");
        if (!confirmed)
        {
            return;
        }

        var res = await _client.PostAsJsonAsync("Library/DeleteReviewQueueItem", rqId);
        if (res.IsSuccessStatusCode)
        {
            bool success = await res.Content.ReadFromJsonAsync<bool>();
            if (!success)
            {
                // todo warn error
            }
            else
            {
                await RefreshRQs();
            }
        }
    }

    public async Task SendUpdateReviewQueueItem(RQ item, string? reason, ReviewQueueStatus reviewQueueStatus)
    {
        var req = new ReqUpdateReviewQueueItem(item.id, reviewQueueStatus, reason);
        var res = await _client.PostAsJsonAsync("Mod/UpdateReviewQueueItem", req);
        if (res.IsSuccessStatusCode)
        {
            var resFindRQ = await _client.PostAsJsonAsync("Library/FindRQ", item.id);
            if (resFindRQ.IsSuccessStatusCode)
            {
                RQ rq = (await resFindRQ.Content.ReadFromJsonAsync<RQ>())!;

                var list = CurrentRQs!.ToList();
                int index = list.IndexOf(item);
                // Console.WriteLine(index);
                list[index] = rq;
                CurrentRQs = list.AsQueryable();
            }
        }
        else
        {
            await _jsRuntime.InvokeVoidAsync("alert",
                $"Error: {res.StatusCode:D} {res.StatusCode} {await res.Content.ReadAsStringAsync()}");
        }

        StateHasChanged();
    }

    private void Onclick_Id(int id)
    {
        _reviewComponentRef.reviewingId = id;
        _reviewComponentRef.Show();
    }
}
