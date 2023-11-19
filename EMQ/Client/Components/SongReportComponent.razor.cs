using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using EMQ.Shared.Library.Entities.Concrete;
using EMQ.Shared.Library.Entities.Concrete.Dto.Request;
using EMQ.Shared.Quiz.Entities.Concrete;
using EMQ.Shared.Quiz.Entities.Concrete.Dto.Request;
using Microsoft.AspNetCore.Components;

namespace EMQ.Client.Components;

public partial class SongReportComponent
{
    [Parameter]
    public Song? Song { get; set; }

    private Blazorise.Modal _modalRef = null!;

    private SongReport ClientSongReport { get; set; } = new();

    public Dictionary<string, bool> SelectedUrls { get; set; } = new();

    public async Task Onclick_Report()
    {
        if (ClientState.Session != null && (Song?.Links.Any() ?? false))
        {
            await _modalRef.Show();
            ClientSongReport = new SongReport
            {
                music_id = Song.Id, submitted_by = ClientState.Session.Player.Username, Song = Song,
            };
            SelectedUrls = Song.Links.ToDictionary(x => x.Url, _ => Song.Links.Count == 1);
        }
        StateHasChanged();
    }

    private async Task SendSongReportReq(SongReport clientSongReport, Dictionary<string, bool> selectedUrls)
    {
        if (SelectedUrls.Any(x => x.Value))
        {
            var req = new ReqSongReport(clientSongReport, selectedUrls);
            HttpResponseMessage res1 = await _client.PostAsJsonAsync("Library/SongReport", req);
            if (res1.IsSuccessStatusCode)
            {
                await _modalRef.Hide();
                StateHasChanged();
            }
        }
    }
}
