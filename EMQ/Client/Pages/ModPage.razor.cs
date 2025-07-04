﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using EMQ.Shared.Auth.Entities.Concrete;
using EMQ.Shared.Core;
using EMQ.Shared.Library.Entities.Concrete;
using EMQ.Shared.Library.Entities.Concrete.Dto.Request;
using EMQ.Shared.Mod.Entities.Concrete.Dto.Request;
using EMQ.Shared.Quiz.Entities.Concrete;
using Microsoft.JSInterop;

namespace EMQ.Client.Pages;

public partial class ModPage
{
    public List<SongReport> SongReports { get; set; } = new();

    public string CountdownMessage { get; set; } = "Server restart in";

    public int CountdownMinutes { get; set; }

    public ServerConfig? ClientServerConfig { get; set; }

    protected override async Task OnInitializedAsync()
    {
        await _clientUtils.TryRestoreSession();
        if (ClientState.Session == null ||
            !AuthStuff.HasPermission(ClientState.Session, PermissionKind.Moderator))
        {
            _navigation.NavigateTo("/", true);
            return;
        }

        ClientServerConfig =
            (await _client.GetFromJsonAsync<ServerStats>($"Auth/GetServerStats?nocache={Guid.NewGuid()}"))!.Config;
        var req = new ReqFindSongReports(DateTime.UtcNow.AddDays(-78), DateTime.UtcNow.AddDays(1));
        var res = await _client.PostAsJsonAsync("Library/FindSongReports", req);
        if (res.IsSuccessStatusCode)
        {
            var songReports = await res.Content.ReadFromJsonAsync<List<SongReport>>();
            foreach (SongReport songReport in songReports!)
            {
                songReport.Song = null;
            }

            SongReports = songReports;
        }
        else
        {
            await _jsRuntime.InvokeVoidAsync("alert",
                $"Error: {res.StatusCode:D} {res.StatusCode} {await res.Content.ReadAsStringAsync()}");
        }
    }

    private async Task Onclick_RunGc()
    {
        HttpResponseMessage res = await _client.PostAsJsonAsync("Mod/RunGc", "");
        if (!res.IsSuccessStatusCode)
        {
            await _jsRuntime.InvokeVoidAsync("alert",
                $"Error: {res.StatusCode:D} {res.StatusCode} {await res.Content.ReadAsStringAsync()}");
        }
    }

    private async Task Onclick_RunAnalysis()
    {
        HttpResponseMessage res = await _client.PostAsJsonAsync("Mod/RunAnalysis", "");
        if (!res.IsSuccessStatusCode)
        {
            await _jsRuntime.InvokeVoidAsync("alert",
                $"Error: {res.StatusCode:D} {res.StatusCode} {await res.Content.ReadAsStringAsync()}");
        }
    }

    private async Task Onclick_StartCountdown()
    {
        var req = new ReqStartCountdown(CountdownMessage, DateTime.UtcNow.AddMinutes(CountdownMinutes));
        HttpResponseMessage res = await _client.PostAsJsonAsync("Mod/StartCountdown", req);
        if (!res.IsSuccessStatusCode)
        {
            await _jsRuntime.InvokeVoidAsync("alert",
                $"Error: {res.StatusCode:D} {res.StatusCode} {await res.Content.ReadAsStringAsync()}");
        }
    }

    private async Task OnClick_DownloadSongLite()
    {
        string res = await _client.GetStringAsync("Mod/ExportSongLite");
        byte[] file = System.Text.Encoding.UTF8.GetBytes(res);
        await _jsRuntime.InvokeVoidAsync("downloadFile", "SongLite.json", "application/json", file);
    }

    private async Task OnClick_DownloadSongLite_MB()
    {
        string res = await _client.GetStringAsync("Mod/ExportSongLite_MB");
        byte[] file = System.Text.Encoding.UTF8.GetBytes(res);
        await _jsRuntime.InvokeVoidAsync("downloadFile", "SongLite_MB.json", "application/json", file);
    }

    private async Task SendSetServerConfigReq()
    {
        HttpResponseMessage res = await _client.PostAsJsonAsync("Mod/SetServerConfig", ClientServerConfig);
        if (!res.IsSuccessStatusCode)
        {
            await _jsRuntime.InvokeVoidAsync("alert",
                $"Error: {res.StatusCode:D} {res.StatusCode} {await res.Content.ReadAsStringAsync()}");
        }

        ClientServerConfig =
            (await _client.GetFromJsonAsync<ServerStats>($"Auth/GetServerStats?nocache={Guid.NewGuid()}"))!.Config;
        StateHasChanged();
    }
}
