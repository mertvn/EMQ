using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Threading.Tasks;
using EMQ.Client.Components;
using EMQ.Shared.Auth.Entities.Concrete;
using EMQ.Shared.Auth.Entities.Concrete.Dto.Request;
using EMQ.Shared.Auth.Entities.Concrete.Dto.Response;
using EMQ.Shared.Core;
using EMQ.Shared.Core.SharedDbEntities;
using EMQ.Shared.Library.Entities.Concrete;
using EMQ.Shared.Quiz.Entities.Concrete;
using EMQ.Shared.VNDB.Business;
using Juliet.Model.Param;
using Juliet.Model.VNDBObject;
using Microsoft.Extensions.Logging;

namespace EMQ.Client.Pages;

public partial class Index
{
    public class LoginModel
    {
        [Required]
        public string UsernameOrEmail { get; set; } = "";

        [Required]
        [StringLength(AuthStuff.MaxPasswordLength, MinimumLength = AuthStuff.MinPasswordLength)]
        public string Password { get; set; } = "";

        [Required]
        public bool IsGuest { get; set; } = false;
    }

    private LoginModel _loginModel = new();

    private List<string> LoginProgressDisplay { get; set; } = new();

    private bool LoginInProgress { get; set; } = false;

    public MyAutocompleteComponent<AutocompleteMst> a { get; set; }

    public AutocompleteMst[] Data { get; set; }

    private TValue[] OnSearch<TValue>(string value)
    {
        value = value.NormalizeForAutocomplete();
        if (string.IsNullOrWhiteSpace(value))
        {
            return Array.Empty<TValue>();
        }

        bool hasNonAscii = Encoding.UTF8.GetByteCount(value) != value.Length;
        const int maxResults = 25; // todo
        var dictLT = new Dictionary<AutocompleteMst, ExtensionMethods.StringMatch>();
        var dictNLT = new Dictionary<AutocompleteMst, ExtensionMethods.StringMatch>();
        foreach (AutocompleteMst d in Data)
        {
            var matchLT = d.MSTLatinTitleNormalized.StartsWithContains(value, StringComparison.Ordinal);
            if (matchLT > 0)
            {
                dictLT[d] = matchLT;
            }

            if (hasNonAscii)
            {
                var matchNLT = d.MSTNonLatinTitleNormalized.StartsWithContains(value, StringComparison.Ordinal);
                if (matchNLT > 0)
                {
                    dictNLT[d] = matchNLT;
                }
            }
        }

        return (TValue[])(object)dictLT.Concat(dictNLT)
            .OrderByDescending(x => x.Value)
            .Take(maxResults)
            .Select(x => x.Key)
            .ToArray();
    }

    protected override async Task OnInitializedAsync()
    {
        LoginInProgress = true;
        StateHasChanged();
        await _clientUtils.TryRestoreSession();
        LoginInProgress = false;
        Data = (await _client.GetFromJsonAsync<AutocompleteMst[]>("autocomplete/mst.json"))!;
        await a.Focus(false);
        StateHasChanged();
    }

    private async Task Logout()
    {
        if (ClientState.Session is not null && !LoginInProgress)
        {
            LoginInProgress = true;
            HttpResponseMessage res = await _client.PostAsJsonAsync("Auth/RemoveSession",
                new ReqRemoveSession(ClientState.Session.Token));
            if (res.IsSuccessStatusCode)
            {
                _logger.LogInformation("Logged out");

                await _clientConnectionManager.StopHubConnection();
                ClientState.Session = null;
                await _clientUtils.SaveSessionToLocalStorage();

                _navigation.NavigateTo("/", forceLoad: true);
            }
            else
            {
                // todo display error
            }

            LoginInProgress = false;
        }
    }

    private async Task Login(LoginModel loginModel)
    {
        if (ClientState.Session is null)
        {
            LoginProgressDisplay = new List<string>();
            LoginInProgress = true;
            StateHasChanged();

            LoginProgressDisplay.Add($"Creating session...");
            StateHasChanged();
            HttpResponseMessage res = await _client.PostAsJsonAsync("Auth/CreateSession",
                new ReqCreateSession(loginModel.UsernameOrEmail, loginModel.Password, loginModel.IsGuest));

            if (res.IsSuccessStatusCode)
            {
                ResCreateSession? resCreateSession = await res.Content.ReadFromJsonAsync<ResCreateSession>();
                if (resCreateSession != null)
                {
                    LoginProgressDisplay.Add($"Created session.");
                    StateHasChanged();

                    ClientState.Session = resCreateSession.Session;
                    ClientState.VndbInfo = resCreateSession.VndbInfo;
                    await _clientUtils.SaveSessionToLocalStorage();

                    _client.DefaultRequestHeaders.TryAddWithoutValidation(AuthStuff.AuthorizationHeaderName,
                        ClientState.Session.Token);

                    LoginProgressDisplay.Add($"Loading preferences...");
                    await _clientUtils.TryRestorePreferences();
                    StateHasChanged();

                    LoginProgressDisplay.Add($"Initializing websocket connection...");
                    StateHasChanged();
                    await _clientConnectionManager.StartManagingConnection();
                    LoginProgressDisplay.Add($"Initialized websocket connection.");
                    StateHasChanged();

                    if (!loginModel.IsGuest)
                    {
                        LoginProgressDisplay.Add($"Fetching song votes...");
                        StateHasChanged();
                        HttpResponseMessage resMusicVote =
                            await _client.PostAsJsonAsync("Auth/GetUserMusicVotes", ClientState.Session.Player.Id);
                        if (resMusicVote.IsSuccessStatusCode)
                        {
                            ClientState.MusicVotes =
                                (await resMusicVote.Content.ReadFromJsonAsync<MusicVote[]>())!.ToDictionary(
                                    x => x.music_id, x => x);
                        }
                    }

                    LoginProgressDisplay.Add($"Successfully logged in.");
                    LoginInProgress = false;
                    StateHasChanged();

                    _navigation.NavigateTo("/HotelPage");
                }
            }
            else
            {
                switch (res.StatusCode)
                {
                    case HttpStatusCode.TooManyRequests:
                        LoginProgressDisplay.Add(
                            $"You have been rate-limited. Try again in {res.Headers.RetryAfter} seconds.");
                        break;
                    default:
                        LoginProgressDisplay.Add("Login failed.");
                        break;
                }

                StateHasChanged();
                LoginInProgress = false;
            }
        }
    }

    private void Onclick_Register()
    {
        _navigation.NavigateTo("/RegisterPage", forceLoad: false);
    }

    private void Onclick_ForgottenPassword()
    {
        _navigation.NavigateTo("/ForgottenPasswordPage", forceLoad: false);
    }

    private async Task Onclick_PlayAsGuest()
    {
        _loginModel.UsernameOrEmail = "Guest";
        _loginModel.Password = "GuestGuestGuestGuest";
        _loginModel.IsGuest = true;
        await Login(_loginModel);
    }
}
