using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Blazorise.DataGrid;
using EMQ.Shared.Library.Entities.Concrete.Dto.Request;
using EMQ.Shared.Quiz.Entities.Concrete;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;

namespace EMQ.Client.Components;

public partial class ReviewQueueComponent
{
    public List<RQ> CurrentRQs { get; set; } = new();

    public string CellStyle { get; set; } =
        "max-width: 240px; overflow: hidden; text-overflow: ellipsis; white-space: nowrap;";

    public string CellStyleInlineBlock { get; set; } =
        "max-width: 240px; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; display: inline-block;";

    protected override async Task OnInitializedAsync()
    {
        await RefreshRQs();
    }

    public async Task RefreshRQs()
    {
        var req = new ReqFindRQs(DateTime.UtcNow.AddDays(-30), DateTime.UtcNow);
        var res = await _client.PostAsJsonAsync("Library/FindRQs", req);
        if (res.IsSuccessStatusCode)
        {
            var content = await res.Content.ReadFromJsonAsync<List<RQ>>();
            if (content is not null)
            {
                content.Reverse();
                CurrentRQs = content;
            }
            else
            {
                _logger.LogError("Failed to find RQs");
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
}
