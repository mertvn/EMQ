using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Blazorise.DataGrid;
using EMQ.Shared.Library.Entities.Concrete.Dto.Request;
using EMQ.Shared.Mod.Entities.Concrete.Dto.Request;
using EMQ.Shared.Quiz.Entities.Concrete;
using Microsoft.AspNetCore.Components.QuickGrid;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;

namespace EMQ.Client.Components;

public partial class ReviewQueueComponent
{
    private readonly PaginationState _pagination = new() { ItemsPerPage = 25 };

    public IQueryable<RQ>? CurrentRQs { get; set; }

    public static HashSet<int> CurrentPendingRQsMIds { get; set; } = new();

    public string CellStyleInlineBlock { get; set; } =
        "max-width: 220px; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; display: inline-block;";

    public string CellStyleInlineBlockShort { get; set; } =
        "max-width: 140px; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; display: inline-block;";

    public DateTime StartDateFilter { get; set; } = DateTime.UtcNow.AddDays(-3);

    public DateTime EndDateFilter { get; set; } = DateTime.UtcNow.AddDays(1);

    protected override async Task OnInitializedAsync()
    {
        await RefreshRQs();
    }

    public async Task RefreshRQs()
    {
        var req = new ReqFindRQs(StartDateFilter, EndDateFilter);
        var res = await _client.PostAsJsonAsync("Library/FindRQs", req);
        if (res.IsSuccessStatusCode)
        {
            var content = await res.Content.ReadFromJsonAsync<List<RQ>>();
            if (content is not null)
            {
                content.Reverse();
                CurrentRQs = content.AsQueryable();
                CurrentPendingRQsMIds = content.Where(x => x.status == ReviewQueueStatus.Pending)
                    .Select(y => y.music_id)
                    .ToHashSet();
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
}
