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
        await SyncChat();
    }

    public void SetTimer()
    {
        Timer.Stop();
        Timer.Elapsed -= OnTimedEvent;

        Timer.Interval = TimeSpan.FromSeconds(20).TotalMilliseconds;
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

    private async Task OnKeyDown(KeyboardEventArgs arg)
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

    private async Task SyncChat()
    {
        ConcurrentQueue<ChatMessage>? chat = null;
        var res = await _client.GetAsync(
            $"Quiz/SyncChat?token={ClientState.Session?.Token}");

        if (res.IsSuccessStatusCode)
        {
            if (res.StatusCode == HttpStatusCode.NoContent)
            {
                chat = null;
            }
            else
            {
                chat = await res.Content.ReadFromJsonAsync<ConcurrentQueue<ChatMessage>>();
            }
        }
        else
        {
            if (res.StatusCode == HttpStatusCode.Unauthorized)
            {
                Timer.Stop();
            }
        }

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
