using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;

namespace EMQ.Client.Pages;

public partial class ModPage
{
    public string AdminPassword { get; set; } = "";

    private async Task Onclick_RunGc()
    {
        HttpResponseMessage res = await _client.PostAsJsonAsync("Mod/RunGc", AdminPassword);
        if (res.IsSuccessStatusCode)
        {
        }
    }
}
