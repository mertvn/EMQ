using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Blazorise.DataGrid;
using EMQ.Client.Pages;
using EMQ.Shared.Core.SharedDbEntities;
using EMQ.Shared.Library.Entities.Concrete.Dto.Request;
using EMQ.Shared.Mod.Entities.Concrete.Dto.Request;
using EMQ.Shared.Quiz.Entities.Concrete;
using Microsoft.AspNetCore.Components.QuickGrid;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;

namespace EMQ.Client.Components;

public partial class EditQueueComponent
{
    private readonly PaginationState _pagination = new() { ItemsPerPage = 25 };

    public IQueryable<EditQueue>? CurrentEQs { get; set; }

    public string CellStyleInlineBlock { get; set; } =
        "max-width: 320px; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; display: inline-block;";

    public string CellStyleInlineBlockShort { get; set; } =
        "max-width: 140px; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; display: inline-block;";

    public DateTime StartDateFilter { get; set; } = DateTime.UtcNow.AddDays(-3);

    public DateTime EndDateFilter { get; set; } = DateTime.UtcNow.AddDays(1);

    protected override async Task OnInitializedAsync()
    {
        await RefreshEQs();
    }

    public async Task RefreshEQs()
    {
        var req = new ReqFindRQs(StartDateFilter, EndDateFilter);
        var res = await _client.PostAsJsonAsync("Library/FindEQs", req);
        if (res.IsSuccessStatusCode)
        {
            var content = await res.Content.ReadFromJsonAsync<List<EditQueue>>();
            if (content is not null)
            {
                content.Reverse();
                CurrentEQs = content.AsQueryable();
            }
            else
            {
                Console.WriteLine("Failed to find EQs");
            }
        }

        StateHasChanged();
    }

    private async Task CallStateHasChanged()
    {
        StateHasChanged();
    }

    private async Task DeleteEditQueueItem(int eqId)
    {
        // todo? generic confirm modal
        bool confirmed = await _jsRuntime.InvokeAsync<bool>("confirm", $"Really delete {eqId}?");
        if (!confirmed)
        {
            return;
        }

        var res = await _client.PostAsJsonAsync("Library/DeleteEditQueueItem", eqId);
        if (res.IsSuccessStatusCode)
        {
            bool success = await res.Content.ReadFromJsonAsync<bool>();
            if (!success)
            {
                // todo warn error
            }
            else
            {
                await RefreshEQs();
            }
        }
    }

    public async Task SendUpdateEditQueueItem(EditQueue item, string? reason,
        ReviewQueueStatus reviewQueueStatus)
    {
        var req = new ReqUpdateReviewQueueItem(item.id, reviewQueueStatus, reason);
        var res = await _client.PostAsJsonAsync("Mod/UpdateEditQueueItem", req);
        if (res.IsSuccessStatusCode)
        {
            var resFindEQ = await _client.PostAsJsonAsync("Library/FindEQ", item.id);
            if (resFindEQ.IsSuccessStatusCode)
            {
                EditQueue
                    eq = (await resFindEQ.Content.ReadFromJsonAsync<EditQueue>())!;

                var list = CurrentEQs!.ToList();
                int index = list.IndexOf(item);
                // Console.WriteLine(index);
                list[index] = eq;
                CurrentEQs = list.AsQueryable();
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
