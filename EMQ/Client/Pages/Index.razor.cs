using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using EMQ.Shared.Auth.Entities.Concrete;
using EMQ.Shared.Auth.Entities.Concrete.Dto.Request;
using EMQ.Shared.Auth.Entities.Concrete.Dto.Response;
using EMQ.Shared.Core;
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
        public string Username { get; set; } = "Guest";

        public string Password { get; set; } = "";

        [RegularExpression(RegexPatterns.VndbIdRegex,
            ErrorMessage = "Invalid VNDB Id: make sure it looks like u1234567")]
        public string? VndbId { get; set; }

        // [MinLength(32, ErrorMessage = "Invalid VNDB API Token")] // prevents login if users enter something random, and then deletes it
        public string? VndbApiToken { get; set; }
    }

    private LoginModel _loginModel = new();

    public List<string> LoginProgressDisplay { get; set; } = new();

    public bool LoginInProgress { get; set; } = false;

    protected override async Task OnInitializedAsync()
    {
        LoginInProgress = true;
        StateHasChanged();
        await _clientUtils.TryRestoreSession();
        LoginInProgress = false;
        StateHasChanged();
    }

    private async Task Logout()
    {
        if (ClientState.Session is not null)
        {
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
        }
    }

    private async Task Login(LoginModel loginModel)
    {
        if (ClientState.Session is null)
        {
            LoginProgressDisplay = new List<string>();
            LoginInProgress = true;
            StateHasChanged();

            if (!string.IsNullOrWhiteSpace(loginModel.VndbApiToken))
            {
                LoginProgressDisplay.Add("Validating VNDB API Token...");
                var resAuth = await Juliet.Api.GET_authinfo(new Param() { APIToken = loginModel.VndbApiToken });
                if (resAuth != null)
                {
                    const string vndbPermName = "listread"; // todo
                    if (!resAuth.Permissions.Contains(vndbPermName))
                    {
                        LoginProgressDisplay.Add(
                            $"Error: VNDB API Token does not have the necessary permissions: {vndbPermName}");
                        LoginInProgress = false;
                        LoginProgressDisplay.Add("Login cancelled.");
                        StateHasChanged();
                        return;
                    }
                    else
                    {
                        LoginProgressDisplay.Add("Successfully validated VNDB API Token.");
                        StateHasChanged();
                    }
                }
                else
                {
                    LoginProgressDisplay.Add("Error: Failed to validate VNDB API Token.");
                    LoginInProgress = false;
                    LoginProgressDisplay.Add("Login cancelled.");
                    StateHasChanged();
                    return;
                }
            }

            List<Label>? vns = null;
            var playerVndbInfo = new PlayerVndbInfo()
            {
                VndbId = loginModel.VndbId, VndbApiToken = loginModel.VndbApiToken, Labels = vns,
            };

            if (!string.IsNullOrWhiteSpace(playerVndbInfo.VndbId))
            {
                LoginProgressDisplay.Add("Grabbing VNs from VNDB...");
                StateHasChanged();

                var labels = new List<Label>();
                VNDBLabel[] vndbLabels = await VndbMethods.GetLabels(playerVndbInfo);

                foreach (VNDBLabel vndbLabel in vndbLabels)
                {
                    labels.Add(Label.FromVndbLabel(vndbLabel));
                }

                // we try including playing, finished, stalled, voted, EMQ-wl, and EMQ-bl labels by default
                foreach (Label label in labels)
                {
                    switch (label.Name.ToLowerInvariant())
                    {
                        case "playing":
                        case "finished":
                        case "stalled":
                        case "voted":
                        case "emq-wl":
                            label.Kind = LabelKind.Include;
                            break;
                        case "emq-bl":
                            label.Kind = LabelKind.Exclude;
                            break;
                    }
                }

                playerVndbInfo.Labels = labels;
                vns = await VndbMethods.GrabPlayerVNsFromVndb(playerVndbInfo);
            }

            if (vns is not null)
            {
                LoginProgressDisplay.Add("Grabbed VNs from VNDB.");
                StateHasChanged();
            }

            LoginProgressDisplay.Add($"Creating session...");
            StateHasChanged();
            HttpResponseMessage res = await _client.PostAsJsonAsync("Auth/CreateSession",
                new ReqCreateSession(
                    loginModel.Username,
                    loginModel.Password,
                    new PlayerVndbInfo() { VndbId = loginModel.VndbId, Labels = vns }));

            if (res.IsSuccessStatusCode)
            {
                ResCreateSession? resCreateSession = await res.Content.ReadFromJsonAsync<ResCreateSession>();
                if (resCreateSession != null)
                {
                    LoginProgressDisplay.Add($"Created session.");
                    StateHasChanged();

                    ClientState.Session = resCreateSession.Session;
                    ClientState.Session.VndbInfo.VndbApiToken = loginModel.VndbApiToken;
                    await _clientUtils.SaveSessionToLocalStorage();

                    _client.DefaultRequestHeaders.TryAddWithoutValidation(AuthStuff.AuthorizationHeaderName,
                        ClientState.Session.Token);

                    LoginProgressDisplay.Add($"Initializing websocket connection...");
                    StateHasChanged();
                    await _clientConnectionManager.StartManagingConnection();
                    LoginProgressDisplay.Add($"Initialized websocket connection.");
                    StateHasChanged();

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
                        LoginProgressDisplay.Add("You have been rate-limited. Try again in a minute.");
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
}
