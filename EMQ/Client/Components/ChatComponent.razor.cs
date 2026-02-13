using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using System.Timers;
using EMQ.Shared.Auth.Entities.Concrete;
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

    public ConcurrentQueue<ChatMessage> ClientChat { get; set; } = new();

    public string ChatInputText { get; set; } = "";

    private ElementReference _chatHistoryRef;

    private bool _preventDefault = false;

    public Timer Timer = new();

    public int ColumnsVw { get; set; } = 14;

    private Dictionary<string, string> EmojiData { get; set; } = new();

    public Player? SelectedPlayer { get; set; }

    private ChatUserDetailComponent _modalRef = null!;

    protected override async Task OnInitializedAsync()
    {
        SetTimer();
        await SyncChat();
        EmojiData = (await _client.GetFromJsonAsync<Dictionary<string, string>>("emoji.json"))!;
    }

    public void Dispose()
    {
        Timer.Dispose();
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
                    bool isCommandMessage = ChatInputText.StartsWith('/');
                    if (isCommandMessage)
                    {
                        ChatMessage? message = null;
                        string[] split = ChatInputText.Split(' ');
                        string commandId = split[0].Replace("/", "").ToLowerInvariant();
                        switch (commandId)
                        {
                            case "roll":
                                {
                                    if (split.Length != 2)
                                    {
                                        message = new ChatMessage("Usage: /roll <number>");
                                    }
                                    else
                                    {
                                        string limitStr = split[1];
                                        if (int.TryParse(limitStr, out int limit))
                                        {
                                            if (limit is > 0 and < int.MaxValue)
                                            {
                                                int rolled = Random.Shared.Next(1, limit + 1);
                                                message = new ChatMessage(rolled.ToString());
                                            }
                                        }
                                    }

                                    break;
                                }
                            case "ping":
                                {
                                    var stopwatch = new Stopwatch();
                                    _ = await _client.GetAsync("Auth/Ping");
                                    stopwatch.Start();
                                    const int n = 2;
                                    for (int i = 0; i < n; i++)
                                    {
                                        var res = await _client.GetAsync("Auth/Ping");
                                        if (!res.IsSuccessStatusCode)
                                        {
                                            throw new Exception();
                                        }
                                    }

                                    message = new ChatMessage($"{stopwatch.ElapsedMilliseconds / n} ms");
                                    stopwatch.Stop();
                                    break;
                                }
                            case "shrug":
                                {
                                    ChatInputText = @"¯\_(ツ)_/¯";
                                    await OnKeyDown(new KeyboardEventArgs() { Key = "Enter" });
                                    return;
                                }
                            default:
                                message = new ChatMessage("Command not found.");
                                break;
                        }

                        ChatInputText = "";
                        if (message != null)
                        {
                            ClientChat.Enqueue(message);
                            await ScrollToEnd();
                            StateHasChanged();
                            await ScrollToEnd();
                            StateHasChanged();
                        }
                    }
                    else
                    {
                        string contents = RegexPatterns.EmojiRegex.Replace(ChatInputText,
                            match => EmojiData.TryGetValue(match.Groups[1].Value, out string? emoji)
                                ? emoji
                                : match.Value);
                        var req = new ReqSendChatMessage(session.Token, contents);
                        ChatInputText = "";
                        var res = await _client.PostAsJsonAsync("Quiz/SendChatMessage", req);
                        if (res.IsSuccessStatusCode)
                        {
                            await SyncChat();
                        }
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

    private void Onclick_Sender(Player sender, MouseEventArgs e)
    {
        bool isChatMod = AuthStuff.HasPermission(ClientState.Session, PermissionKind.ModerateChat);
        if (!isChatMod)
        {
            return;
        }

        SelectedPlayer = sender;
        _modalRef.Show(e.ClientX, e.ClientY + 30);
    }
}
