﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net.Http.Json;
using System.Threading.Tasks;
using EMQ.Shared.Core;
using EMQ.Shared.Quiz.Entities.Concrete;
using EMQ.Shared.Quiz.Entities.Concrete.Dto.Request;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;

namespace EMQ.Client.Components;

public partial class ChatComponent
{
    [Parameter]
    public ConcurrentQueue<ChatMessage> Chat { get; set; } = new();

    [Parameter]
    public Func<Task>? Callback { get; set; }

    public string ChatInputText { get; set; } = "";

    private ElementReference _chatHistoryRef;

    private bool _preventDefault = false;

    private async void OnKeyDown(KeyboardEventArgs arg)
    {
        if (arg.Key is "Enter" or "NumpadEnter")
        {
            _preventDefault = true;

            var session = ClientState.Session;
            if (session is { RoomId: { } } && !string.IsNullOrWhiteSpace(ChatInputText))
            {
                if (ChatInputText.Length < Constants.MaxChatMessageLength)
                {
                    var req = new ReqSendChatMessage(session.Token, session.RoomId.Value, ChatInputText);
                    var res = await _client.PostAsJsonAsync("Quiz/SendChatMessage", req);
                    if (res.IsSuccessStatusCode)
                    {
                        ChatInputText = "";
                        Callback?.Invoke();
                    }
                }
                else
                {
                    // todo warn user somehow
                }
            }
        }
        else
        {
            _preventDefault = false;
        }
    }

    public async Task ScrollToEnd()
    {
        await _jsRuntime.InvokeVoidAsync("scrollToEnd", _chatHistoryRef);
        StateHasChanged();
    }
}