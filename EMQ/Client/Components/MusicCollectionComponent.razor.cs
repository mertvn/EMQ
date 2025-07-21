using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using EMQ.Shared.Auth.Entities.Concrete.Dto.Response;
using EMQ.Shared.Core;
using EMQ.Shared.Core.SharedDbEntities;
using EMQ.Shared.Library.Entities.Concrete;
using EMQ.Shared.Library.Entities.Concrete.Dto;
using EMQ.Shared.Library.Entities.Concrete.Dto.Request;
using EMQ.Shared.Mod.Entities.Concrete.Dto.Request;
using EMQ.Shared.Quiz.Entities.Concrete;
using EMQ.Shared.Quiz.Entities.Concrete.Dto.Request;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;

namespace EMQ.Client.Components;

public partial class MusicCollectionComponent
{
    [Parameter]
    public Func<Task>? ParentStateHasChangedCallback { get; set; }

    [Parameter]
    public Song Song { get; set; } = new();

    private Blazorise.Modal _modalRef = null!;

    private bool IsOpen { get; set; }

    private IQueryable<CollectionContainer>? CurrentCollectionContainers { get; set; }

    private ResGetCollectionContainers? ResGetCollectionContainers { get; set; }

    private async Task RefreshComments()
    {
        ResGetCollectionContainers = null;
        CurrentCollectionContainers = null;

        var reqCollections = new CollectionContainer(new Collection() { entity_kind = EntityKind.Song, name = "7"},
            new List<CollectionUsers>(),
            new List<CollectionEntity>() { new() { entity_id = Song.Id } });
        var resCollections = await _client.PostAsJsonAsync("Library/GetEntityCollections",
            new List<CollectionContainer>() { reqCollections });
        if (resCollections.IsSuccessStatusCode)
        {
            HttpResponseMessage resCollectionContainers =
                await _client.PostAsJsonAsync("Library/GetCollectionContainers",
                    await resCollections.Content.ReadFromJsonAsync<int[]>());
            if (resCollectionContainers.IsSuccessStatusCode)
            {
                ResGetCollectionContainers =
                    (await resCollectionContainers.Content.ReadFromJsonAsync<ResGetCollectionContainers>())!;
                CurrentCollectionContainers = ResGetCollectionContainers!.CollectionContainers.AsQueryable();
            }
        }
    }

    private async Task OnOpened()
    {
        // Console.WriteLine("OnOpened");
        IsOpen = true;
        await RefreshComments();
    }

    private async Task OnClosed()
    {
        // Console.WriteLine("OnClosed");
        IsOpen = false;
    }

    public void Show()
    {
        StateHasChanged();
        _modalRef.Show();
    }
}
