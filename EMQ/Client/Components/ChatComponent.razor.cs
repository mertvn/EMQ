using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using System.Timers;
using EMQ.Shared.Core;
using EMQ.Shared.Quiz.Entities.Concrete;
using EMQ.Shared.Quiz.Entities.Concrete.Dto.Request;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;

namespace EMQ.Client.Components;

public partial class ChatComponent
{
    public ConcurrentQueue<ChatMessage> Chat { get; set; } = new();

    public string ChatInputText { get; set; } = "";

    private ElementReference _chatHistoryRef;

    private bool _preventDefault = false;

    public Timer Timer = new();

    protected override async Task OnInitializedAsync()
    {
        SetTimer();
    }

    public void SetTimer()
    {
        Timer.Stop();
        Timer.Elapsed -= OnTimedEvent;

        // todo increase this interval after making sure Room can get WS chat updates too
        Timer.Interval = TimeSpan.FromSeconds(1).TotalMilliseconds;
        Timer.Elapsed += OnTimedEvent;
        Timer.AutoReset = true;
        Timer.Start();
    }

    public async void OnTimedEvent(object? sender, ElapsedEventArgs e)
    {
        if (Timer.Enabled)
        {
            await SyncChat();
        }
    }

    private async void OnKeyDown(KeyboardEventArgs arg)
    {
        if (arg.Key is "Enter" or "NumpadEnter")
        {
            _preventDefault = true;

            var session = ClientState.Session;
            if (session != null && !string.IsNullOrWhiteSpace(ChatInputText))
            {
                if (ChatInputText.Length <= Constants.MaxChatMessageLength)
                {
                    var req = new ReqSendChatMessage(session.Token, ChatInputText);
                    ChatInputText = "";
                    var res = await _client.PostAsJsonAsync("Quiz/SendChatMessage", req);
                    if (res.IsSuccessStatusCode)
                    {
                        await SyncChat();
                    }
                }
            }
        }
        else
        {
            _preventDefault = false;
        }
    }

    // todo: do this with messages sent from the server instead of polling if signalr ever gets reliable enough
    private async Task SyncChat()
    {
        ConcurrentQueue<ChatMessage>? chat = null;
        var res = await _client.GetAsync(
            $"Quiz/SyncChat?token={ClientState.Session?.Token}");

        if (res.StatusCode == HttpStatusCode.NoContent)
            chat = null;
        else if (res.IsSuccessStatusCode)
            chat = await res.Content.ReadFromJsonAsync<ConcurrentQueue<ChatMessage>>();

        if (chat is not null)
        {
            if (chat.Count > Chat.Count)
            {
                Chat = chat;

                // need to call twice or it doesn't scroll all the way to the end /shrug
                await ScrollToEnd();
                StateHasChanged();
                await ScrollToEnd();
                StateHasChanged();
            }
        }
    }

    public async Task ScrollToEnd()
    {
        try
        {
            await _jsRuntime.InvokeVoidAsync("scrollToEnd", _chatHistoryRef);
            StateHasChanged();
        }
        catch (Exception e)
        {
            Console.WriteLine(e.ToString());
        }
    }

    public async Task CallStateHasChanged()
    {
        await ScrollToEnd();
        StateHasChanged();
        await ScrollToEnd();
        StateHasChanged();
    }
}
