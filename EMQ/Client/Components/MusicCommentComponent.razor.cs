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
using EMQ.Shared.Library.Entities.Concrete.Dto.Request;
using EMQ.Shared.Mod.Entities.Concrete.Dto.Request;
using EMQ.Shared.Quiz.Entities.Concrete;
using EMQ.Shared.Quiz.Entities.Concrete.Dto.Request;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;

namespace EMQ.Client.Components;

public partial class MusicCommentComponent
{
    [Parameter]
    public Func<Task>? ParentStateHasChangedCallback { get; set; }

    [Parameter]
    public Song Song { get; set; } = new();

    private Blazorise.Modal _modalRef = null!;

    private bool IsOpen { get; set; }

    public MusicComment ClientComment { get; set; } = new();

    private IQueryable<MusicComment>? CurrentMusicComments { get; set; }

    private ResGetMusicComments? ResGetMusicComments { get; set; }

    private async Task RefreshComments()
    {
        ResGetMusicComments = null;
        CurrentMusicComments = null;
        var res = await _client.PostAsJsonAsync("Library/GetMusicComments", Song.Id);
        if (res.IsSuccessStatusCode)
        {
            ResGetMusicComments = await res.Content.ReadFromJsonAsync<ResGetMusicComments>();
            CurrentMusicComments = ResGetMusicComments!.MusicComments.AsQueryable();
            ClientComment = new MusicComment { music_id = Song.Id };
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

    private async Task Onclick_SubmitComment()
    {
        var req = ClientComment;
        var res = await _client.PostAsJsonAsync("Library/InsertMusicComment", req);
        if (res.IsSuccessStatusCode)
        {
            await RefreshComments();
        }
        else
        {
            await _jsRuntime.InvokeVoidAsync("alert",
                $"Error: {res.StatusCode:D} {res.StatusCode} {await res.Content.ReadAsStringAsync()}");
        }
    }

    private void SetUrl(bool value, string url)
    {
        // very inefficient, but it is what it is
        ClientComment.urls = value
            ? ClientComment.urls.Concat(new[] { url }).ToArray()
            : ClientComment.urls.Except(new[] { url }).ToArray();
    }

    private async Task DeleteComment(int id)
    {
        // todo? generic confirm modal
        bool confirmed = await _jsRuntime.InvokeAsync<bool>("confirm", $"Really delete comment?");
        if (!confirmed)
        {
            return;
        }

        var res = await _client.PostAsJsonAsync("Library/DeleteMusicComment", id);
        if (res.IsSuccessStatusCode)
        {
            await RefreshComments();
        }
        else
        {
            await _jsRuntime.InvokeVoidAsync("alert",
                $"Error: {res.StatusCode:D} {res.StatusCode} {await res.Content.ReadAsStringAsync()}");
        }
    }
}
