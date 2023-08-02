using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using EMQ.Shared.Library.Entities.Concrete;
using EMQ.Shared.Library.Entities.Concrete.Dto.Request;

namespace EMQ.Client.Pages;

public partial class ModPage
{
    public string AdminPassword { get; set; } = "";

    public List<SongReport> SongReports { get; set; } = new();

    protected override async Task OnInitializedAsync()
    {
        var req = new ReqFindSongReports(DateTime.UtcNow.AddDays(-30), DateTime.UtcNow);
        var res = await _client.PostAsJsonAsync("Library/FindSongReports", req);
        if (res.IsSuccessStatusCode)
        {
            var songReports = await res.Content.ReadFromJsonAsync<List<SongReport>>();
            foreach (SongReport songReport in songReports!)
            {
                songReport.Song = null!;
            }

            SongReports = songReports;
        }
    }

    private async Task Onclick_RunGc()
    {
        HttpResponseMessage res = await _client.PostAsJsonAsync("Mod/RunGc", AdminPassword);
        if (res.IsSuccessStatusCode)
        {
        }
    }
}
