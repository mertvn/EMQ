﻿using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using Dapper;
using EMQ.Client;
using EMQ.Server.Db;
using EMQ.Server.Db.Entities;
using EMQ.Shared.Quiz.Entities.Concrete;
using EMQ.Server.Hubs;
using EMQ.Shared.Auth.Entities.Concrete;
using EMQ.Shared.Core;
using EMQ.Shared.Core.SharedDbEntities;
using EMQ.Shared.VNDB.Business;
using Microsoft.AspNetCore.SignalR;
using Npgsql;

namespace EMQ.Server.Business;

public class QuizManager
{
    public QuizManager(Quiz quiz)
    {
        Quiz = quiz;
    }

    public Quiz Quiz { get; }

    private DateTime LastUpdate { get; set; }

    // initialized by PrimeQuiz()
    private Dictionary<GuessKind, Dictionary<int, List<string>>> CorrectAnswersDicts { get; set; } = new();

    private FrozenDictionary<int, string[]>? ArtistAliasesDict { get; set; }

    private FrozenDictionary<int, string[]>? ArtistBandsDict { get; set; }

    private DateTime PreviousGuessPhaseStartedAt { get; set; }

    private async Task SetTimer()
    {
        if (!Quiz.IsDisposed && !Quiz.IsTimerRunning)
        {
            Quiz.Timer?.Dispose();
            Quiz.Timer = new PeriodicTimer(TimeSpan.FromMilliseconds(Quiz.TickRate));
            Quiz.IsTimerRunning = true;
            while (await Quiz.Timer.WaitForNextTickAsync())
            {
                await OnTimedEvent();
            }
        }
    }

    private async Task OnTimedEvent()
    {
        if (!Quiz.IsTimerRunning)
        {
            await Task.Delay(TimeSpan.FromMilliseconds(100));
            return;
        }

        if (Quiz.QuizState.QuizStatus == QuizStatus.Playing)
        {
            // var tickStart = DateTime.UtcNow; // todo? this might not be precise enough
            if (Quiz.QuizState.Phase != QuizPhaseKind.Looting && DateTime.UtcNow - LastUpdate > TimeSpan.FromSeconds(1))
            {
                // Console.WriteLine($"sending update at {DateTime.UtcNow}");
                LastUpdate = DateTime.UtcNow;
                TypedQuizHub.ReceiveUpdateRoom(Quiz.Room.Players.Concat(Quiz.Room.Spectators).Select(x => x.Id),
                    Quiz.Room, false);
            }

            if (Quiz.QuizState.RemainingMs >= 0)
            {
                Quiz.QuizState.RemainingMs -= Quiz.TickRate;
            }

            if (Quiz.QuizState.RemainingMs <= 0)
            {
                if (!Quiz.IsDisposed)
                {
                    Quiz.IsTimerRunning = false;
                }

                await WaitForLaggingPlayers();
                switch (Quiz.QuizState.Phase)
                {
                    case QuizPhaseKind.Guess:
                        await EnterJudgementPhase();
                        break;
                    case QuizPhaseKind.Judgement:
                        await EnterResultsPhase();
                        break;
                    case QuizPhaseKind.Results:
                        await EnterGuessingPhase();
                        break;
                    case QuizPhaseKind.Looting:
                        bool lootingSuccess = await SetLootedSongs();
                        await EnterQuiz();

                        if (!lootingSuccess)
                        {
                            Quiz.Room.Log("Canceling quiz due to looting failure", writeToChat: true);
                            await CancelQuiz();
                            return;
                        }

                        await EnterGuessingPhase();
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }

                if (!Quiz.IsDisposed)
                {
                    Quiz.IsTimerRunning = true;
                }
            }

            // var tickEnd = DateTime.UtcNow;
            // double tickMs = (tickEnd - tickStart).TotalMilliseconds;
            // if (Quiz.QuizState.QuizStatus == QuizStatus.Playing && Quiz.QuizState.sp > 0 && tickMs > Quiz.TickRate)
            // {
            //     // Console.WriteLine($"Can't keep up! {tickMs} ms");
            // }
        }
    }

    public async Task CancelQuiz()
    {
        Quiz.QuizState.QuizStatus = QuizStatus.Canceled;

        if (!Quiz.IsDisposed)
        {
            Quiz.IsTimerRunning = false;
            Quiz.Timer?.Dispose();
        }

        TypedQuizHub.ReceiveQuizCanceled(Quiz.Room.Players.Concat(Quiz.Room.Spectators).Select(x => x.Id));
    }

    private async Task EnterGuessingPhase()
    {
        while (Quiz.QuizState.IsPaused)
        {
            await Task.Delay(TimeSpan.FromSeconds(1));
        }

        while (Quiz.Room.QuizSettings.GamemodeKind == GamemodeKind.NGMC &&
               Quiz.Room.Players.Any(x => x.NGMCMustPick || x.NGMCMustBurn))
        {
            Quiz.QuizState.ExtraInfo = "Waiting for NGMC decisions...";

            TypedQuizHub.ReceiveUpdateRoom(Quiz.Room.Players.Concat(Quiz.Room.Spectators).Select(x => x.Id), Quiz.Room,
                false);
            await Task.Delay(TimeSpan.FromSeconds(1));
        }

        if (Quiz.Room.QuizSettings.GamemodeKind == GamemodeKind.Radio && Quiz.QuizState.sp >= 0)
        {
            Quiz.QuizState.RemainingMs = (float)((PreviousGuessPhaseStartedAt.AddMilliseconds(SongLink
                                                      .GetShortestLink(Quiz.Songs[Quiz.QuizState.sp].Links,
                                                          Quiz.Room.QuizSettings.Filters.IsPreferLongLinks).Duration
                                                      .TotalMilliseconds) -
                                                  DateTime.UtcNow).TotalMilliseconds +
                                                 TimeSpan.FromSeconds(3).TotalMilliseconds);
            while (Quiz.QuizState.RemainingMs > 0)
            {
                Quiz.QuizState.ExtraInfo = $"Waiting for the song to end...";
                Quiz.QuizState.RemainingMs -= 1000;
                await Task.Delay(1000);
                TypedQuizHub.ReceiveUpdateRoom(Quiz.Room.Players.Concat(Quiz.Room.Spectators).Select(x => x.Id),
                    Quiz.Room, false);
            }
        }

        PreviousGuessPhaseStartedAt = DateTime.UtcNow;

        int isBufferedCount = Quiz.Room.Players.Count(x => x.IsBuffered);
        int timeoutMs = Quiz.Room.QuizSettings.TimeoutMs;
        // Console.WriteLine("ibc " + isBufferedCount);

        int activePlayersCount = ServerState.Sessions.Where(x => Quiz.Room.Players.Any(y => y.Id == x.Player.Id))
            .Count(x => x.Player.HasActiveConnectionQuiz);
        // Room.Log($"activePlayers: {activePlayers}/{Quiz.Room.Players.Count}");

        float waitNumber = (float)Math.Round(
            activePlayersCount * ((float)Quiz.Room.QuizSettings.WaitPercentage / 100),
            MidpointRounding.AwayFromZero);

        while (isBufferedCount < waitNumber &&
               timeoutMs > 0)
        {
            // Console.WriteLine("in while " + isBufferedCount + "/" + waitNumber);
            await Task.Delay(1000);
            timeoutMs -= 1000;

            isBufferedCount = Quiz.Room.Players.Count(x => x.IsBuffered);

            activePlayersCount = ServerState.Sessions.Where(x => Quiz.Room.Players.Any(y => y.Id == x.Player.Id))
                .Count(x => x.Player.HasActiveConnectionQuiz);

            waitNumber = (float)Math.Round(
                activePlayersCount * ((float)Quiz.Room.QuizSettings.WaitPercentage / 100),
                MidpointRounding.AwayFromZero);

            Quiz.QuizState.ExtraInfo =
                $"Waiting buffering... {isBufferedCount}/{waitNumber} timeout in {timeoutMs / 1000}s";
            // Console.WriteLine("ei: " + Quiz.QuizState.ExtraInfo);

            TypedQuizHub.ReceiveUpdateRoom(Quiz.Room.Players.Concat(Quiz.Room.Spectators).Select(x => x.Id), Quiz.Room,
                false);
        }

        Quiz.QuizState.ExtraInfo = "";
        while (Quiz.QuizState.IsPaused)
        {
            // todo this fucks up timing of radio
            await Task.Delay(TimeSpan.FromSeconds(1));
        }

        Quiz.QuizState.Phase = QuizPhaseKind.Guess;
        Quiz.QuizState.RemainingMs = Quiz.Room.QuizSettings.GuessMs;
        Quiz.QuizState.sp += 1;
        Quiz.Songs[Quiz.QuizState.sp].PlayedAt = DateTime.UtcNow;
        Quiz.QuizState.TeamGuessesHaveBeenDetermined = false;

        foreach (var player in Quiz.Room.Players)
        {
            player.Guess = null;
            player.IsGuessKindCorrectDict = null;
            player.PlayerStatus = PlayerStatus.Thinking;
            player.IsBuffered = false;
            player.IsSkipping = false;
            player.IsReadiedUp = player.IsBot;
        }

        // reset the guesses
        TypedQuizHub.ReceivePlayerGuesses(Quiz.Room.Players.Concat(Quiz.Room.Spectators).Select(x => x.Id),
            Quiz.Room.PlayerGuesses);

        TypedQuizHub.ReceiveUpdateRoom(Quiz.Room.Players.Concat(Quiz.Room.Spectators).Select(x => x.Id), Quiz.Room,
            true);
    }

    private async Task EnterJudgementPhase()
    {
        Quiz.QuizState.Phase = QuizPhaseKind.Judgement;
        Quiz.QuizState.ExtraInfo = "";

        await DetermineBotGuesses();

        TypedQuizHub.ReceiveUpdateRoom(Quiz.Room.Players.Concat(Quiz.Room.Spectators).Select(x => x.Id), Quiz.Room,
            true);

        if (Quiz.Room.QuizSettings.IsSharedGuessesTeams)
        {
            await Task.Delay(TimeSpan.FromMilliseconds(500)); // wait for late guesses (especially non-Entered guesses)
            DetermineTeamGuesses();
        }

        // need to do this AFTER the team guesses have been determined
        TypedQuizHub.ReceivePlayerGuesses(Quiz.Room.Players.Concat(Quiz.Room.Spectators).Select(x => x.Id),
            Quiz.Room.PlayerGuesses);

        await JudgeGuesses();
    }

    private async Task DetermineBotGuesses()
    {
        var song = Quiz.Songs[Quiz.QuizState.sp];
        foreach (var bot in Quiz.Room.Players.Where(x => x.IsBot))
        {
            foreach ((GuessKind guessKind, bool _) in Quiz.Room.QuizSettings.EnabledGuessKinds.Where(x => x.Value))
            {
                bool isCorrect;
                switch (bot.BotInfo!.BotKind)
                {
                    case PlayerBotKind.Default:
                        {
                            float hitChance = song.Stats.GetValueOrDefault(guessKind)?.CorrectPercentage ?? 0;
                            switch (bot.BotInfo!.Difficulty)
                            {
                                case SongDifficultyLevel.VeryEasy:
                                    hitChance *= 0.2f;
                                    break;
                                case SongDifficultyLevel.Easy:
                                    hitChance *= 0.5f;
                                    break;
                                case SongDifficultyLevel.Medium:
                                    break;
                                case SongDifficultyLevel.Hard:
                                    hitChance *= 1.5f;
                                    break;
                                case SongDifficultyLevel.VeryHard:
                                    hitChance *= 2f;
                                    break;
                                case SongDifficultyLevel.Impossible:
                                    hitChance = 100;
                                    break;
                                default:
                                    throw new ArgumentOutOfRangeException();
                            }

                            hitChance = Math.Clamp(hitChance, 0, 100);

                            // todo
                            if (guessKind == GuessKind.Mst)
                            {
                                bot.BotInfo.LastSongHitChance = hitChance;
                            }

                            // bot.BotInfo.SongHitChanceDict[song.Id] = hitChance;
                            isCorrect = Random.Shared.NextDouble() * 100 <= hitChance;
                            break;
                        }
                    case PlayerBotKind.Mimic:
                        {
                            float hitChance = 0;
                            if (bot.BotInfo.SongHitChanceDict.TryGetValue(song.Id, out var hitChanceDict))
                            {
                                if (hitChanceDict != null && hitChanceDict.TryGetValue(guessKind, out hitChance))
                                {
                                    hitChance = Math.Clamp(hitChance, 0, 100);

                                    // todo
                                    if (guessKind == GuessKind.Mst)
                                    {
                                        bot.BotInfo.LastSongHitChance = hitChance;
                                    }
                                }
                            }

                            isCorrect = Random.Shared.NextDouble() * 100 <= hitChance;
                            break;
                        }
                    default:
                        throw new ArgumentOutOfRangeException();
                }

                if (isCorrect)
                {
                    // todo? artist answers might not work with all settings
                    string guess = guessKind switch
                    {
                        GuessKind.Mst => Converters.GetSingleTitle(song.Sources.First().Titles).LatinTitle,
                        GuessKind.A => Converters.GetSingleTitle(song.Artists.First().Titles).LatinTitle,
                        GuessKind.Mt => Converters.GetSingleTitle(song.Titles).LatinTitle,
                        GuessKind.Developer => song.Sources.First().Developers.FirstOrDefault()?.Title.LatinTitle ?? "",
                        GuessKind.Composer => Converters.GetSingleTitle(
                            song.Artists.FirstOrDefault(x => x.Roles.Contains(SongArtistRole.Composer))?.Titles ??
                            new List<Title> { new() }).LatinTitle,
                        GuessKind.Arranger => Converters.GetSingleTitle(
                            song.Artists.FirstOrDefault(x => x.Roles.Contains(SongArtistRole.Arranger))?.Titles ??
                            new List<Title> { new() }).LatinTitle,
                        GuessKind.Lyricist => Converters.GetSingleTitle(
                            song.Artists.FirstOrDefault(x => x.Roles.Contains(SongArtistRole.Lyricist))?.Titles ??
                            new List<Title> { new() }).LatinTitle,
                        _ => ""
                    };

                    await OnSendGuessChanged(bot.Id, guess, guessKind);
                }
                else
                {
                    // todo? set guess to something random
                }
            }
        }
    }

    private void DetermineTeamGuesses()
    {
        Quiz.QuizState.TeamGuessesHaveBeenDetermined = true;
        Dictionary<GuessKind, HashSet<int>> processedTeamIdsDict =
            Enum.GetValues<GuessKind>().ToDictionary(x => x, _ => new HashSet<int>());
        foreach (Player player in Quiz.Room.Players)
        {
            if (player.Guess is null)
            {
                continue;
            }

            var isGuessCorrect = IsGuessCorrect(player.Guess);
            foreach ((GuessKind key, bool? value) in isGuessCorrect)
            {
                // todo this is inefficient, do it in a batched manner
                var processedTeamIds = processedTeamIdsDict[key];
                if (!processedTeamIds.Contains(player.TeamId) && value!.Value)
                {
                    processedTeamIds.Add(player.TeamId);
                    foreach (Player possibleTeammate in Quiz.Room.Players)
                    {
                        possibleTeammate.Guess ??= new PlayerGuess();
                        if (possibleTeammate.TeamId == player.TeamId)
                        {
                            // Console.WriteLine($"setting {possibleTeammate.Username}'s guess.Mst ({possibleTeammate.Guess.Mst} to {player.Username}'s guess.Mst ({player.Guess.Mst})");
                            possibleTeammate.Guess.Dict[key] = player.Guess.Dict[key];
                        }
                    }
                }
            }
        }

        TypedQuizHub.ReceiveUpdateRoom(Quiz.Room.Players.Concat(Quiz.Room.Spectators).Select(x => x.Id), Quiz.Room,
            false);
    }

    /// the value is actually guaranteed to be non-nullable, it's only marked as nullable to match the type of the property on Player
    private Dictionary<GuessKind, bool?> IsGuessCorrect(PlayerGuess? playerGuess)
    {
        // todo? only check enabled types
        var dict = Enum.GetValues<GuessKind>().ToDictionary<GuessKind, GuessKind, bool?>(x => x, _ => false);
        if (playerGuess == null)
        {
            return dict;
        }

        {
            string? guess = playerGuess.Dict[GuessKind.Mst];
            bool correct = false;
            if (!string.IsNullOrWhiteSpace(guess))
            {
                if (!CorrectAnswersDicts[GuessKind.Mst].TryGetValue(Quiz.QuizState.sp, out var correctAnswers))
                {
                    correctAnswers = Quiz.Songs[Quiz.QuizState.sp].Sources.SelectMany(x => x.Titles)
                        .Select(x => x.LatinTitle).ToList();
                    correctAnswers.AddRange(Quiz.Songs[Quiz.QuizState.sp].Sources.SelectMany(x => x.Titles)
                        .Select(x => x.NonLatinTitle).Where(x => !string.IsNullOrWhiteSpace(x))!);
                    correctAnswers = correctAnswers.Select(x => x.Replace(" ", "").Replace("　", ""))
                        .Distinct().ToList();

                    CorrectAnswersDicts[GuessKind.Mst].Add(Quiz.QuizState.sp, correctAnswers);
                    Quiz.Room.Log("cA: " + JsonSerializer.Serialize(correctAnswers, Utils.Jso));
                }

                foreach (string correctAnswer in correctAnswers)
                {
                    if (string.Equals(guess.Trim().Replace(" ", "").Replace("　", ""),
                            correctAnswer, StringComparison.InvariantCultureIgnoreCase))
                    {
                        correct = true;
                        break;
                    }
                }
            }

            dict[GuessKind.Mst] = correct;
        }

        {
            string? guess = playerGuess.Dict[GuessKind.A];
            bool correct = false;
            if (!string.IsNullOrWhiteSpace(guess))
            {
                if (!CorrectAnswersDicts[GuessKind.A].TryGetValue(Quiz.QuizState.sp, out var correctAnswers))
                {
                    if (ArtistAliasesDict != null)
                    {
                        IEnumerable<SongArtist> artists = Quiz.Songs[Quiz.QuizState.sp].Artists;
                        if (!Quiz.Room.QuizSettings.IsTreatNonVocalsAsCorrect)
                        {
                            artists = artists.Where(x => x.Roles.Contains(SongArtistRole.Vocals));
                        }

                        correctAnswers = new List<string>();
                        var aIds = artists.Select(x => x.Id);
                        foreach (int aId in aIds)
                        {
                            correctAnswers.AddRange(ArtistAliasesDict[aId].Select(x => x.NormalizeForAutocomplete()));
                            correctAnswers.AddRange(ArtistAliasesDict[aId].Select(x => Utils.GetReversedArtistName(x)));
                            if (ArtistBandsDict != null)
                            {
                                if (ArtistBandsDict.TryGetValue(aId, out string[]? bandMemberAliases))
                                {
                                    correctAnswers.AddRange(bandMemberAliases.Select(x =>
                                        x.NormalizeForAutocomplete()));
                                    correctAnswers.AddRange(bandMemberAliases.Select(x =>
                                        Utils.GetReversedArtistName(x)));
                                }
                            }
                        }
                    }
                    else
                    {
                        IEnumerable<SongArtist> artists = Quiz.Songs[Quiz.QuizState.sp].Artists;
                        if (!Quiz.Room.QuizSettings.IsTreatNonVocalsAsCorrect)
                        {
                            artists = artists.Where(x => x.Roles.Contains(SongArtistRole.Vocals));
                        }

                        var titles = artists.SelectMany(x => x.Titles).ToArray();
                        correctAnswers = titles.Select(x => x.LatinTitle.NormalizeForAutocomplete()).ToList();
                        correctAnswers.AddRange(titles.Select(x => x.NonLatinTitle?.NormalizeForAutocomplete())
                            .Where(x => !string.IsNullOrWhiteSpace(x))!);

                        correctAnswers.AddRange(titles.Select(x => Utils.GetReversedArtistName(x.LatinTitle)));
                        correctAnswers.AddRange(titles.Select(x => Utils.GetReversedArtistName(x.NonLatinTitle))
                            .Where(x => !string.IsNullOrWhiteSpace(x)));
                    }

                    correctAnswers = correctAnswers.Distinct().ToList();
                    CorrectAnswersDicts[GuessKind.A].Add(Quiz.QuizState.sp, correctAnswers);
                    Quiz.Room.Log("cA-a: " + JsonSerializer.Serialize(correctAnswers, Utils.Jso));
                }

                foreach (string correctAnswer in correctAnswers)
                {
                    if (guess.NormalizeForAutocomplete() == correctAnswer)
                    {
                        correct = true;
                        break;
                    }
                }
            }

            dict[GuessKind.A] = correct;
        }

        {
            string? guess = playerGuess.Dict[GuessKind.Mt];
            bool correct = false;
            if (!string.IsNullOrWhiteSpace(guess))
            {
                if (!CorrectAnswersDicts[GuessKind.Mt].TryGetValue(Quiz.QuizState.sp, out var correctAnswers))
                {
                    correctAnswers = Quiz.Songs[Quiz.QuizState.sp].Titles
                        .Select(x => x.LatinTitle).ToList();
                    correctAnswers.AddRange(Quiz.Songs[Quiz.QuizState.sp].Titles
                        .Select(x => x.NonLatinTitle).Where(x => !string.IsNullOrWhiteSpace(x))!);
                    correctAnswers = correctAnswers.Select(x => x.NormalizeForAutocomplete()).Distinct().ToList();

                    CorrectAnswersDicts[GuessKind.Mt].Add(Quiz.QuizState.sp, correctAnswers);
                    Quiz.Room.Log("cA-mt: " + JsonSerializer.Serialize(correctAnswers, Utils.Jso));
                }

                foreach (string correctAnswer in correctAnswers)
                {
                    if (guess.NormalizeForAutocomplete() == correctAnswer)
                    {
                        correct = true;
                        break;
                    }
                }
            }

            dict[GuessKind.Mt] = correct;
        }

        {
            string? guess = playerGuess.Dict[GuessKind.Rigger];
            bool correct = false;
            if (!string.IsNullOrWhiteSpace(guess))
            {
                if (!CorrectAnswersDicts[GuessKind.Rigger].TryGetValue(Quiz.QuizState.sp, out var correctAnswers))
                {
                    var riggerIds = Quiz.Songs[Quiz.QuizState.sp].PlayerLabels
                        .Where(x => x.Value.Any(y => y.Kind == LabelKind.Include)).Select(z => z.Key);
                    correctAnswers = Quiz.Room.Players.Where(x => riggerIds.Contains(x.Id)).Select(y => y.Username)
                        .ToList();
                    correctAnswers = correctAnswers.Select(x => x.NormalizeForAutocomplete()).Distinct().ToList();

                    CorrectAnswersDicts[GuessKind.Rigger].Add(Quiz.QuizState.sp, correctAnswers);
                    Quiz.Room.Log("cA-rigger: " + JsonSerializer.Serialize(correctAnswers, Utils.Jso));
                }

                foreach (string correctAnswer in correctAnswers)
                {
                    if (guess.NormalizeForAutocomplete() == correctAnswer)
                    {
                        correct = true;
                        break;
                    }
                }
            }

            dict[GuessKind.Rigger] = correct;
        }

        {
            string? guess = playerGuess.Dict[GuessKind.Developer];
            bool correct = false;
            if (!string.IsNullOrWhiteSpace(guess))
            {
                if (!CorrectAnswersDicts[GuessKind.Developer].TryGetValue(Quiz.QuizState.sp, out var correctAnswers))
                {
                    correctAnswers = new List<string>();
                    foreach (var developer in Quiz.Songs[Quiz.QuizState.sp].Sources.SelectMany(x => x.Developers))
                    {
                        correctAnswers.Add(developer.Title.LatinTitle);
                        if (!string.IsNullOrWhiteSpace(developer.Title.NonLatinTitle))
                        {
                            correctAnswers.Add(developer.Title.NonLatinTitle);
                        }
                    }

                    correctAnswers = correctAnswers.Select(x => x.NormalizeForAutocomplete()).Distinct().ToList();

                    CorrectAnswersDicts[GuessKind.Developer].Add(Quiz.QuizState.sp, correctAnswers);
                    Quiz.Room.Log("cA-developer: " + JsonSerializer.Serialize(correctAnswers, Utils.Jso));
                }

                foreach (string correctAnswer in correctAnswers)
                {
                    if (guess.NormalizeForAutocomplete() == correctAnswer)
                    {
                        correct = true;
                        break;
                    }
                }
            }

            dict[GuessKind.Developer] = correct;
        }

        {
            const GuessKind guessKind = GuessKind.Composer;
            string? guess = playerGuess.Dict[guessKind];
            bool correct = false;
            if (!string.IsNullOrWhiteSpace(guess))
            {
                if (!CorrectAnswersDicts[guessKind].TryGetValue(Quiz.QuizState.sp, out var correctAnswers))
                {
                    var songArtistsWithRole = Quiz.Songs[Quiz.QuizState.sp].Artists
                        .Where(x => x.Roles.Contains(SongArtistRole.Composer)).ToArray();
                    if (ArtistAliasesDict != null)
                    {
                        correctAnswers = new List<string>();
                        var aIds = songArtistsWithRole.Select(x => x.Id);
                        foreach (int aId in aIds)
                        {
                            correctAnswers.AddRange(ArtistAliasesDict[aId].Select(x => x.NormalizeForAutocomplete()));
                            if (ArtistBandsDict != null)
                            {
                                if (ArtistBandsDict.TryGetValue(aId, out string[]? bandMemberAliases))
                                {
                                    correctAnswers.AddRange(bandMemberAliases.Select(x =>
                                        x.NormalizeForAutocomplete()));
                                }
                            }
                        }
                    }
                    else
                    {
                        var titles = songArtistsWithRole.SelectMany(x => x.Titles).ToArray();
                        correctAnswers = titles.Select(x => x.LatinTitle.NormalizeForAutocomplete()).ToList();
                        correctAnswers.AddRange(titles.Select(x => x.NonLatinTitle?.NormalizeForAutocomplete())
                            .Where(x => !string.IsNullOrWhiteSpace(x))!);
                    }

                    correctAnswers = correctAnswers.Distinct().ToList();
                    CorrectAnswersDicts[guessKind].Add(Quiz.QuizState.sp, correctAnswers);
                    Quiz.Room.Log("cA-composer: " + JsonSerializer.Serialize(correctAnswers, Utils.Jso));
                }

                foreach (string correctAnswer in correctAnswers)
                {
                    if (guess.NormalizeForAutocomplete() == correctAnswer)
                    {
                        correct = true;
                        break;
                    }
                }
            }

            dict[guessKind] = correct;
        }

        {
            const GuessKind guessKind = GuessKind.Arranger;
            string? guess = playerGuess.Dict[guessKind];
            bool correct = false;
            if (!string.IsNullOrWhiteSpace(guess))
            {
                if (!CorrectAnswersDicts[guessKind].TryGetValue(Quiz.QuizState.sp, out var correctAnswers))
                {
                    var songArtistsWithRole = Quiz.Songs[Quiz.QuizState.sp].Artists
                        .Where(x => x.Roles.Contains(SongArtistRole.Arranger)).ToArray();
                    if (ArtistAliasesDict != null)
                    {
                        correctAnswers = new List<string>();
                        var aIds = songArtistsWithRole.Select(x => x.Id);
                        foreach (int aId in aIds)
                        {
                            correctAnswers.AddRange(ArtistAliasesDict[aId].Select(x => x.NormalizeForAutocomplete()));
                            if (ArtistBandsDict != null)
                            {
                                if (ArtistBandsDict.TryGetValue(aId, out string[]? bandMemberAliases))
                                {
                                    correctAnswers.AddRange(bandMemberAliases.Select(x =>
                                        x.NormalizeForAutocomplete()));
                                }
                            }
                        }
                    }
                    else
                    {
                        var titles = songArtistsWithRole.SelectMany(x => x.Titles).ToArray();
                        correctAnswers = titles.Select(x => x.LatinTitle.NormalizeForAutocomplete()).ToList();
                        correctAnswers.AddRange(titles.Select(x => x.NonLatinTitle?.NormalizeForAutocomplete())
                            .Where(x => !string.IsNullOrWhiteSpace(x))!);
                    }

                    correctAnswers = correctAnswers.Distinct().ToList();
                    CorrectAnswersDicts[guessKind].Add(Quiz.QuizState.sp, correctAnswers);
                    Quiz.Room.Log("cA-arranger: " + JsonSerializer.Serialize(correctAnswers, Utils.Jso));
                }

                foreach (string correctAnswer in correctAnswers)
                {
                    if (guess.NormalizeForAutocomplete() == correctAnswer)
                    {
                        correct = true;
                        break;
                    }
                }
            }

            dict[guessKind] = correct;
        }

        {
            const GuessKind guessKind = GuessKind.Lyricist;
            string? guess = playerGuess.Dict[guessKind];
            bool correct = false;
            if (!string.IsNullOrWhiteSpace(guess))
            {
                if (!CorrectAnswersDicts[guessKind].TryGetValue(Quiz.QuizState.sp, out var correctAnswers))
                {
                    var songArtistsWithRole = Quiz.Songs[Quiz.QuizState.sp].Artists
                        .Where(x => x.Roles.Contains(SongArtistRole.Lyricist)).ToArray();
                    if (ArtistAliasesDict != null)
                    {
                        correctAnswers = new List<string>();
                        var aIds = songArtistsWithRole.Select(x => x.Id);
                        foreach (int aId in aIds)
                        {
                            correctAnswers.AddRange(ArtistAliasesDict[aId].Select(x => x.NormalizeForAutocomplete()));
                            if (ArtistBandsDict != null)
                            {
                                if (ArtistBandsDict.TryGetValue(aId, out string[]? bandMemberAliases))
                                {
                                    correctAnswers.AddRange(bandMemberAliases.Select(x =>
                                        x.NormalizeForAutocomplete()));
                                }
                            }
                        }
                    }
                    else
                    {
                        var titles = songArtistsWithRole.SelectMany(x => x.Titles).ToArray();
                        correctAnswers = titles.Select(x => x.LatinTitle.NormalizeForAutocomplete()).ToList();
                        correctAnswers.AddRange(titles.Select(x => x.NonLatinTitle?.NormalizeForAutocomplete())
                            .Where(x => !string.IsNullOrWhiteSpace(x))!);
                    }

                    correctAnswers = correctAnswers.Distinct().ToList();
                    CorrectAnswersDicts[guessKind].Add(Quiz.QuizState.sp, correctAnswers);
                    Quiz.Room.Log("cA-lyricist: " + JsonSerializer.Serialize(correctAnswers, Utils.Jso));
                }

                foreach (string correctAnswer in correctAnswers)
                {
                    if (guess.NormalizeForAutocomplete() == correctAnswer)
                    {
                        correct = true;
                        break;
                    }
                }
            }

            dict[guessKind] = correct;
        }

        return dict;
    }

    private async Task WaitForLaggingPlayers()
    {
        await Utils.WaitWhile(() =>
        {
            bool needToWait = false;
            var playerIds = Quiz.Room.Players.Where(x => x.HasActiveConnectionQuiz).Select(x => x.Id);
            foreach (int playerId in playerIds)
            {
                if (ServerState.PumpMessages.TryGetValue(playerId, out var queue))
                {
                    // Console.WriteLine(queue.Count);
                    if (queue.SendingTasks.Count > 1)
                    {
                        // Console.WriteLine($"needToWait for p{playerId}");
                        Quiz.QuizState.ExtraInfo = "Waiting for lagging players...";
                        needToWait = true;
                    }
                }
            }

            return Task.FromResult(needToWait);
        }, 160, Quiz.Room.QuizSettings.MaxWaitForLaggingPlayersMs);

        Quiz.QuizState.ExtraInfo = "";
    }

    private async Task EnterResultsPhase()
    {
        TypedQuizHub.ReceiveCorrectAnswer(Quiz.Room.Players.Concat(Quiz.Room.Spectators).Select(x => x.Id),
            Quiz.Songs[Quiz.QuizState.sp],
            Quiz.Songs[Quiz.QuizState.sp].PlayerLabels,
            Quiz.Room.PlayerGuesses,
            Quiz.Songs[Quiz.QuizState.sp].PlayerVotes);

        // wait a little for these messages to reach players, otherwise it looks really janky
        await Task.Delay(TimeSpan.FromMilliseconds(160));

        // send answers again to make sure everyone receives late answers
        TypedQuizHub.ReceivePlayerGuesses(Quiz.Room.Players.Concat(Quiz.Room.Spectators).Select(x => x.Id),
            Quiz.Room.PlayerGuesses);

        // wait a little for these messages to reach players, otherwise it looks really janky
        await Task.Delay(TimeSpan.FromMilliseconds(160));

        Quiz.QuizState.Phase = QuizPhaseKind.Results;
        Quiz.QuizState.RemainingMs = Quiz.Room.QuizSettings.ResultsMs;

        foreach (var player in Quiz.Room.Players)
        {
            player.IsBuffered = false;
            player.IsSkipping = false;
        }

        TypedQuizHub.ReceiveUpdateRoom(Quiz.Room.Players.Concat(Quiz.Room.Spectators).Select(x => x.Id), Quiz.Room,
            true);

        if (Quiz.QuizState.sp + 1 == Quiz.Songs.Count)
        {
            await EndQuiz();
        }
        else if (Quiz.Room.QuizSettings.MaxLives > 0)
        {
            var teams = Quiz.Room.Players.GroupBy(x => x.TeamId).ToArray();
            bool isOneTeamGame = teams.Length == 1;
            if (isOneTeamGame)
            {
                if (teams.Single().First().Lives <= 0)
                {
                    await EndQuiz();
                }
            }
            else
            {
                var teamsWithLives = teams.Where(x => x.Any(y => y.Lives > 0)).ToArray();
                bool onlyOneTeamWithLivesLeft = teamsWithLives.Length == 1;
                if (onlyOneTeamWithLivesLeft)
                {
                    Quiz.Room.Log($"Team {teamsWithLives.Single().First().TeamId} won!", writeToChat: true);
                    await EndQuiz();
                }
                else if (teamsWithLives.Length == 0)
                {
                    await EndQuiz();
                }
            }
        }

        if (Quiz.Room.HotjoinQueue.Any())
        {
            while (Quiz.Room.HotjoinQueue.Any())
            {
                if (Quiz.Room.HotjoinQueue.TryDequeue(out Player? player))
                {
                    player.Lives = Quiz.Room.QuizSettings.MaxLives;
                    player.Score = 0;
                    player.AnsweringKind = Quiz.Room.QuizSettings.AnsweringKind == AnsweringKind.Mixed
                        ? player.AnsweringKind
                        : Quiz.Room.QuizSettings.AnsweringKind;
                    player.Guess = null;
                    Quiz.Room.Players.Enqueue(player);
                    Quiz.Room.RemoveSpectator(player);
                    Quiz.Room.Log($"{player.Username} hotjoined.", player.Id, true);

                    if (Quiz.Room.QuizSettings.IsSharedGuessesTeams)
                    {
                        Quiz.Room.QuizSettings.TeamSize = Math.Clamp(1, Quiz.Room.QuizSettings.TeamSize + 1, 777);
                    }
                }
            }

            TypedQuizHub.ReceiveUpdateRoom(Quiz.Room.Players.Concat(Quiz.Room.Spectators).Select(x => x.Id), Quiz.Room,
                false);
        }

        if (Quiz.Room.QuizSettings.GamemodeKind == GamemodeKind.NGMC)
        {
            // bot picks/burns
            var qm = ServerState.QuizManagers.First(x => x.Quiz.Id == Quiz.Id);
            foreach (Player bot in Quiz.Room.Players.Where(x => x.IsBot && (x.NGMCMustPick || x.NGMCMustBurn)))
            {
                if (bot.NGMCMustPick)
                {
                    // todo? smarter algo
                    var pickedPlayer = Quiz.Room.Players.Last(x => x.TeamId == bot.TeamId && x.NGMCCanBePicked);
                    await qm.NGMCPickPlayer(pickedPlayer, bot, false);
                }
                else if (bot.NGMCMustBurn)
                {
                    // todo? smarter algo
                    var halfGuessPlayer = Quiz.Room.Players.LastOrDefault(x =>
                        x.TeamId == bot.TeamId && x.NGMCGuessesCurrent >= 0.5f &&
                        (Math.Abs((int)x.NGMCGuessesCurrent - x.NGMCGuessesCurrent) > 0.01f));
                    if (halfGuessPlayer != null)
                    {
                        await qm.NGMCBurnPlayer(halfGuessPlayer, bot);
                    }
                    else if (Random.Shared.NextSingle() < 0.5f)
                    {
                        var burnedPlayer = Quiz.Room.Players.Last(x =>
                            x.TeamId == bot.TeamId && x.NGMCGuessesCurrent >= 0.5f);
                        await qm.NGMCBurnPlayer(burnedPlayer, bot);
                    }
                    else
                    {
                        bot.NGMCCanBurn = false;
                        bot.NGMCMustBurn = false;
                        Quiz.Room.Log($"{bot.Username} skipped burning.", writeToChat: true);
                        TypedQuizHub.ReceiveUpdateRoom(
                            Quiz.Room.Players.Concat(Quiz.Room.Spectators).Select(x => x.Id), Quiz.Room,
                            false);
                    }
                }
            }
        }
    }

    private async Task JudgeGuesses()
    {
        // don't make this delay configurable (at least not for regular users)
        await Task.Delay(TimeSpan.FromMilliseconds(900)); // add suspense & wait for late guesses

        var song = Quiz.Songs[Quiz.QuizState.sp];
        var songHistory = new SongHistory { Song = song };
        var startTime = TimeSpan.FromSeconds(song.StartTime);
        var duration = song.DetermineSongStartTimeGetDuration(Quiz.Room.QuizSettings.Filters);

        // todo? take guesskinds as a param to avoid selecting unnecessary information
        var userSongStatsLookup =
            await DbManager.GetSHPlayerSongStats(new List<int> { song.Id },
                Quiz.Room.Players.Select(x => x.Id).ToList());

        int enabledGuessKindsCount = Quiz.Room.QuizSettings.EnabledGuessKinds.Count(x => x.Value);
        foreach (var player in Quiz.Room.Players)
        {
            if (Quiz.Room.QuizSettings.IsPreventSameSongSpam)
            {
                player.SongLastPlayedAtDict[song.Id] = DateTime.UtcNow;
            }

            if (Quiz.Room.QuizSettings.IsPreventSameVNSpam)
            {
                foreach (SongSource source in song.Sources)
                {
                    // todo make this work for non-VNDB
                    var vndbLink = source.Links.SingleOrDefault(x => x.Type == SongSourceLinkType.VNDB);
                    if (vndbLink != null)
                    {
                        player.VNLastPlayedAtDict[vndbLink.Url] = DateTime.UtcNow;
                    }
                }
            }

            if (player.PlayerStatus == PlayerStatus.Dead)
            {
                continue;
            }

            Quiz.Room.Log("pG: " + player.Guess, player.Id);

            int correctCount = 0;
            player.IsGuessKindCorrectDict = IsGuessCorrect(player.Guess);
            foreach ((GuessKind _, bool? value) in player.IsGuessKindCorrectDict)
            {
                if (value!.Value)
                {
                    correctCount += 1;
                }
            }

            player.Score += correctCount;
            player.PlayerStatus = correctCount > 0 ? PlayerStatus.Correct : PlayerStatus.Wrong;

            int wrongCount = enabledGuessKindsCount - correctCount;
            if (wrongCount > 0 && Quiz.Room.QuizSettings.MaxLives > 0 && player.Lives >= 0)
            {
                switch (Quiz.Room.QuizSettings.GamemodeKind)
                {
                    case GamemodeKind.NGMC:
                    case GamemodeKind.EruMode:
                        break;
                    case GamemodeKind.Default:
                    case GamemodeKind.Radio:
                    default:
                        player.Lives -= Quiz.Room.QuizSettings.LivesScoringKind switch
                        {
                            LivesScoringKind.Default => 1,
                            LivesScoringKind.EachGuessTypeTakesOneLife => wrongCount,
                            _ => throw new ArgumentOutOfRangeException()
                        };

                        break;
                }

                if (player.Lives <= 0)
                {
                    player.PlayerStatus = PlayerStatus.Dead;
                }
            }

            if (player.HasActiveConnectionQuiz)
            {
                UserSpacedRepetition? previous = null;
                UserSpacedRepetition? current = null;

                bool doSpacedRepetition = Quiz.Room.QuizSettings.GamemodeKind != GamemodeKind.Radio;
                if (doSpacedRepetition)
                {
                    try
                    {
                        (previous, current) = await DoSpacedRepetition(player.Id, song.Id,
                            player.IsGuessKindCorrectDict[GuessKind.Mst]!.Value); // todo?
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine("Failed to DoSpacedRepetition");
                        Console.WriteLine(e);
                    }
                }

                _ = song.PlayerLabels.TryGetValue(player.Id, out var labels);

                foreach ((GuessKind key, bool _) in Quiz.Room.QuizSettings.EnabledGuessKinds.Where(x => x.Value))
                {
                    PlayerSongStats? userSongStats = null;
                    if (userSongStatsLookup.Contains(player.Id))
                    {
                        userSongStats = userSongStatsLookup[player.Id].Single().GetValueOrDefault(key)
                            ?.GetValueOrDefault(song.Id);
                    }

                    userSongStats ??= new PlayerSongStats { UserId = player.Id, MusicId = song.Id, GuessKind = key };
                    var guessInfo = new GuessInfo
                    {
                        Username = player.Username,
                        Guess = player.Guess?.Dict[key] ?? "",
                        FirstGuessMs = player.Guess?.DictFirstGuessMs[key] ?? 0,
                        IsGuessCorrect = player.IsGuessKindCorrectDict[key]!.Value,
                        Labels = key == GuessKind.Mst ? labels : null,
                        IsOnList = labels?.Any() ?? false,
                        PreviousUserSpacedRepetition = key == GuessKind.Mst ? previous : null,
                        CurrentUserSpacedRepetition = key == GuessKind.Mst ? current : null,
                        PlayerSongStats = userSongStats,
                        StartTime = startTime,
                        Duration = duration,
                    };

                    if (!songHistory.PlayerGuessInfos.TryGetValue(player.Id, out var dict))
                    {
                        dict = new Dictionary<GuessKind, GuessInfo>();
                        songHistory.PlayerGuessInfos[player.Id] = dict;
                    }

                    dict[key] = guessInfo;
                }
            }
        }

        Quiz.SongsHistory[Quiz.QuizState.sp] = songHistory;

        switch (Quiz.Room.QuizSettings.GamemodeKind)
        {
            case GamemodeKind.NGMC:
                {
                    var teams = Quiz.Room.Players.GroupBy(x => x.TeamId).ToArray();
                    var team1 = teams.ElementAt(0);
                    var team2 = teams.ElementAt(1);

                    var team1CorrectPlayers = team1
                        .Where(x => x.NGMCGuessesCurrent >= 1f && x.PlayerStatus == PlayerStatus.Correct)
                        .ToArray();
                    var team2CorrectPlayers = team2
                        .Where(x => x.NGMCGuessesCurrent >= 1f && x.PlayerStatus == PlayerStatus.Correct)
                        .ToArray();

                    foreach (Player correctPlayer in team1CorrectPlayers.Concat(team2CorrectPlayers))
                    {
                        correctPlayer.NGMCCanBePicked = true;
                    }

                    int team1CorrectPlayersCount = team1CorrectPlayers.Length;
                    int team2CorrectPlayersCount = team2CorrectPlayers.Length;
                    var team1Captain = team1.First();
                    var team2Captain = team2.First();

                    if (team1CorrectPlayersCount > 0)
                    {
                        if (!team2CorrectPlayers.Any())
                        {
                            foreach (Player player in team2)
                            {
                                player.Lives -= 1;
                            }
                        }

                        if (Quiz.Room.QuizSettings.NGMCAutoPickOnlyCorrectPlayerInTeam && team1CorrectPlayersCount == 1)
                        {
                            await NGMCPickPlayer(team1CorrectPlayers.Single(), team1Captain, true);
                        }
                        else
                        {
                            team1Captain.NGMCMustPick = true;
                        }
                    }

                    if (team2CorrectPlayersCount > 0)
                    {
                        if (!team1CorrectPlayers.Any())
                        {
                            foreach (Player player in team1)
                            {
                                player.Lives -= 1;
                            }
                        }

                        if (Quiz.Room.QuizSettings.NGMCAutoPickOnlyCorrectPlayerInTeam && team2CorrectPlayersCount == 1)
                        {
                            await NGMCPickPlayer(team2CorrectPlayers.Single(), team2Captain, true);
                        }
                        else
                        {
                            team2Captain.NGMCMustPick = true;
                        }
                    }

                    team1Captain.NGMCCanBurn = Quiz.Room.QuizSettings.NGMCAllowBurning && !team1CorrectPlayers.Any();
                    team2Captain.NGMCCanBurn = Quiz.Room.QuizSettings.NGMCAllowBurning && !team2CorrectPlayers.Any();
                    team1Captain.NGMCMustBurn = team1Captain.NGMCCanBurn;
                    team2Captain.NGMCMustBurn = team2Captain.NGMCCanBurn;

                    string team1GuessesStr = string.Join(";", team1.Select(x => x.NGMCGuessesCurrent));
                    string team2GuessesStr = string.Join(";", team2.Select(x => x.NGMCGuessesCurrent));
                    Quiz.Room.Log($"{team1GuessesStr} | {team2GuessesStr} {team1.First().Lives}-{team2.First().Lives}",
                        writeToChat: true);
                    break;
                }
            case GamemodeKind.EruMode:
                {
                    EruModeTick();
                    break;
                }
            case GamemodeKind.Default:
            case GamemodeKind.Radio:
            default:
                break;
        }
    }

    public void EruModeTick()
    {
        var enabledGuessKinds =
            Quiz.Room.QuizSettings.EnabledGuessKinds.Where(x => x.Value).Select(x => x.Key).ToHashSet();
        var teams = Quiz.Room.Players
            .Where(p => p.Lives > 0) // dead players can't take lives
            .GroupBy(x => x.TeamId)
            .Select(g => g.ToArray())
            .ToArray();
        for (int i = 0; i < teams.Length; i++)
        {
            var currentTeam = teams[i];
            for (int j = i + 1; j < teams.Length; j++)
            {
                var opposingTeam = teams[j];
                int minPlayerCount = Math.Min(currentTeam.Length, opposingTeam.Length); // todo option to use max?
                for (int k = 0; k < minPlayerCount; k++)
                {
                    Player currentPlayer = currentTeam[k];
                    Player opposingPlayer = opposingTeam[k];

                    var currentWrong = currentPlayer.IsGuessKindCorrectDict?
                        .Where(x => enabledGuessKinds.Contains(x.Key) && x.Value.HasValue && !x.Value.Value)
                        .Select(x => x.Key)
                        .ToHashSet() ?? new HashSet<GuessKind>();

                    var opposingWrong = opposingPlayer.IsGuessKindCorrectDict?
                        .Where(x => enabledGuessKinds.Contains(x.Key) && x.Value.HasValue && !x.Value.Value)
                        .Select(x => x.Key)
                        .ToHashSet() ?? new HashSet<GuessKind>();

                    var currentTakes = opposingWrong.Except(currentWrong).ToList();
                    if (currentTakes.Count > 0)
                    {
                        int livesToTake = Quiz.Room.QuizSettings.LivesScoringKind switch
                        {
                            LivesScoringKind.Default => 1,
                            LivesScoringKind.EachGuessTypeTakesOneLife => currentTakes.Count,
                            _ => throw new ArgumentOutOfRangeException()
                        };

                        Quiz.Room.Log(
                            $"{currentPlayer.Username} took {livesToTake} lives from {opposingPlayer.Username} for: {string.Join(", ", currentTakes.Select(x => x.GetDescription()))}.",
                            writeToChat: true);
                        foreach (Player player in opposingTeam)
                        {
                            player.Lives -= livesToTake;
                        }
                    }

                    var opposingTakes = currentWrong.Except(opposingWrong).ToList();
                    if (opposingTakes.Count > 0)
                    {
                        int livesToTake = Quiz.Room.QuizSettings.LivesScoringKind switch
                        {
                            LivesScoringKind.Default => 1,
                            LivesScoringKind.EachGuessTypeTakesOneLife => opposingTakes.Count,
                            _ => throw new ArgumentOutOfRangeException()
                        };

                        Quiz.Room.Log(
                            $"{opposingPlayer.Username} took {livesToTake} lives from {currentPlayer.Username} for: {string.Join(", ", opposingTakes.Select(x => x.GetDescription()))}.",
                            writeToChat: true);
                        foreach (Player player in currentTeam)
                        {
                            player.Lives -= livesToTake;
                        }
                    }
                }
            }
        }

        Quiz.Room.Log($"{string.Join("-", teams.Select(x => x.First().Lives))}", writeToChat: true);
    }

    public async Task NGMCBurnPlayer(Player burnedPlayer, Player requestingPlayer)
    {
        if (Quiz.QuizState.Phase is QuizPhaseKind.Judgement or QuizPhaseKind.Looting)
        {
            return;
        }

        if (Quiz.QuizState.Phase is QuizPhaseKind.Guess)
        {
            bool firstSec = (Quiz.Room.QuizSettings.GuessMs - Quiz.QuizState.RemainingMs) < 1000;
            if (!firstSec)
            {
                return;
            }
        }

        if (Quiz.QuizState.QuizStatus != QuizStatus.Playing)
        {
            return;
        }

        if (burnedPlayer.TeamId != requestingPlayer.TeamId)
        {
            return;
        }

        var teams = Quiz.Room.Players.GroupBy(x => x.TeamId).ToArray();
        var team1 = teams.ElementAt(0);
        var team2 = teams.ElementAt(1);

        var burnedPlayerTeam = burnedPlayer.TeamId == 1 ? team1 : team2;
        var burnedPlayerTeamFirstPlayer = burnedPlayerTeam.First();
        if (burnedPlayerTeamFirstPlayer.NGMCCanBurn && burnedPlayer.NGMCGuessesCurrent > 0)
        {
            burnedPlayerTeamFirstPlayer.NGMCCanBurn = false;
            burnedPlayerTeamFirstPlayer.NGMCMustBurn = false;
            burnedPlayer.NGMCGuessesCurrent -= 0.5f;
            Quiz.Room.Log($"{requestingPlayer.Username} burned {burnedPlayer.Username}.", writeToChat: true);

            if (burnedPlayerTeam.All(x => x.NGMCGuessesCurrent == 0))
            {
                foreach (Player player in burnedPlayerTeam)
                {
                    player.NGMCGuessesCurrent = player.NGMCGuessesInitial;
                }

                Quiz.Room.Log($"Resetting guesses for team {burnedPlayer.TeamId}.", writeToChat: true);
            }

            string team1GuessesStr = string.Join(";", team1.Select(x => x.NGMCGuessesCurrent));
            string team2GuessesStr = string.Join(";", team2.Select(x => x.NGMCGuessesCurrent));
            Quiz.Room.Log($"{team1GuessesStr} | {team2GuessesStr} {team1.First().Lives}-{team2.First().Lives}",
                writeToChat: true);

            TypedQuizHub.ReceiveUpdateRoom(Quiz.Room.Players.Concat(Quiz.Room.Spectators).Select(x => x.Id), Quiz.Room,
                false);
        }
    }

    public async Task NGMCDontBurn(Player requestingPlayer)
    {
        if (Quiz.QuizState.Phase is QuizPhaseKind.Judgement or QuizPhaseKind.Looting)
        {
            return;
        }

        if (Quiz.QuizState.Phase is QuizPhaseKind.Guess)
        {
            bool firstSec = (Quiz.Room.QuizSettings.GuessMs - Quiz.QuizState.RemainingMs) < 1000;
            if (!firstSec)
            {
                return;
            }
        }

        if (Quiz.QuizState.QuizStatus != QuizStatus.Playing)
        {
            return;
        }

        var teams = Quiz.Room.Players.GroupBy(x => x.TeamId).ToArray();
        var burnedPlayerTeam = teams.ElementAt(requestingPlayer.TeamId - 1);
        var burnedPlayerTeamFirstPlayer = burnedPlayerTeam.First();
        if (burnedPlayerTeamFirstPlayer.NGMCCanBurn)
        {
            burnedPlayerTeamFirstPlayer.NGMCCanBurn = false;
            burnedPlayerTeamFirstPlayer.NGMCMustBurn = false;

            Quiz.Room.Log($"{requestingPlayer.Username} skipped burning.", writeToChat: true);
            TypedQuizHub.ReceiveUpdateRoom(Quiz.Room.Players.Concat(Quiz.Room.Spectators).Select(x => x.Id), Quiz.Room,
                false);
        }
    }

    public async Task NGMCPickPlayer(Player pickedPlayer, Player requestingPlayer, bool isAutoPick)
    {
        if (!isAutoPick && Quiz.QuizState.Phase is QuizPhaseKind.Judgement or QuizPhaseKind.Looting)
        {
            return;
        }

        if (Quiz.QuizState.Phase is QuizPhaseKind.Guess)
        {
            bool firstSec = (Quiz.Room.QuizSettings.GuessMs - Quiz.QuizState.RemainingMs) < 1000;
            if (!firstSec)
            {
                return;
            }
        }

        if (Quiz.QuizState.QuizStatus != QuizStatus.Playing)
        {
            return;
        }

        if (pickedPlayer.TeamId != requestingPlayer.TeamId)
        {
            return;
        }

        var teams = Quiz.Room.Players.GroupBy(x => x.TeamId).ToArray();
        var team1 = teams.ElementAt(0);
        var team2 = teams.ElementAt(1);

        var pickedPlayerTeam = pickedPlayer.TeamId == 1 ? team1 : team2;
        var pickedPlayerTeamFirstPlayer = pickedPlayerTeam.First();
        if (pickedPlayer.NGMCCanBePicked)
        {
            foreach (Player player in pickedPlayerTeam)
            {
                player.NGMCCanBePicked = false;
            }

            pickedPlayerTeamFirstPlayer.NGMCMustPick = false;
            pickedPlayer.NGMCGuessesCurrent -= 1;
            Quiz.Room.Log($"{requestingPlayer.Username} picked {pickedPlayer.Username}.", writeToChat: true);

            if (pickedPlayerTeam.All(x => x.NGMCGuessesCurrent == 0))
            {
                foreach (Player player in pickedPlayerTeam)
                {
                    player.NGMCGuessesCurrent = player.NGMCGuessesInitial;
                }

                Quiz.Room.Log($"Resetting guesses for team {pickedPlayer.TeamId}.", writeToChat: true);
            }

            if (!isAutoPick)
            {
                string team1GuessesStr = string.Join(";", team1.Select(x => x.NGMCGuessesCurrent));
                string team2GuessesStr = string.Join(";", team2.Select(x => x.NGMCGuessesCurrent));
                Quiz.Room.Log($"{team1GuessesStr} | {team2GuessesStr} {team1.First().Lives}-{team2.First().Lives}",
                    writeToChat: true);
            }

            TypedQuizHub.ReceiveUpdateRoom(Quiz.Room.Players.Concat(Quiz.Room.Spectators).Select(x => x.Id), Quiz.Room,
                false);
        }
    }

    public async Task EndQuiz()
    {
        if (Quiz.QuizState.QuizStatus == QuizStatus.Ended)
        {
            return;
        }

        Quiz.QuizState.QuizStatus = QuizStatus.Ended;
        Quiz.Room.Log("Ended");

        if (!Quiz.IsDisposed)
        {
            Quiz.IsTimerRunning = false;
            Quiz.Timer?.Dispose();
        }

        Quiz.QuizState.ExtraInfo = "Quiz ended. Returning to room...";
        TypedQuizHub.ReceiveQuizEnded(Quiz.Room.Players.Concat(Quiz.Room.Spectators).Select(x => x.Id));

        Directory.CreateDirectory("RoomLog");
        await File.WriteAllTextAsync($"RoomLog/r{Quiz.Room.Id}q{Quiz.Id}.json",
            JsonSerializer.Serialize(Quiz.Room.RoomLog, Utils.JsoIndented));

        if (!Quiz.SongsHistory.Any())
        {
            return;
        }

        // Directory.CreateDirectory("SongHistory");
        // await File.WriteAllTextAsync(
        //     $"SongHistory/SongHistory_{Utils.FixFileName(Quiz.Room.Name)}_r{Quiz.Room.Id}q{Quiz.Id}.json",
        //     JsonSerializer.Serialize(Quiz.SongsHistory, Utils.JsoIndented));

        var entityQuiz = new EntityQuiz
        {
            id = Quiz.Id,
            room_id = Quiz.Room.Id,
            settings_b64 = Quiz.Room.QuizSettings.SerializeToBase64String_PB(),
            should_update_stats = Quiz.Room.QuizSettings.ShouldUpdateStats,
            created_at = Quiz.CreatedAt,
        };

        try
        {
            long _ = await DbManager.InsertEntity(entityQuiz);
        }
        catch (Exception e)
        {
            Console.WriteLine($"Failed to insert Quiz: {JsonSerializer.Serialize(entityQuiz, Utils.JsoIndented)}");
            Console.WriteLine(e);
        }

        var quizSongHistories = new List<QuizSongHistory>();
        foreach ((int sp, SongHistory? songHistory) in Quiz.SongsHistory)
        {
            // Console.WriteLine(JsonSerializer.Serialize(songHistory.PlayerGuessInfos, Utils.JsoIndented));
            bool hasDeveloper = songHistory.Song.Sources.Any(x => x.Developers.Any());
            bool hasComposer = songHistory.Song.Artists.Any(x => x.Roles.Contains(SongArtistRole.Composer));
            bool hasArranger = songHistory.Song.Artists.Any(x => x.Roles.Contains(SongArtistRole.Arranger));
            bool hasLyricist = songHistory.Song.Artists.Any(x => x.Roles.Contains(SongArtistRole.Lyricist));

            var startTime = TimeSpan.FromSeconds(songHistory.Song.StartTime);
            var duration = new Song() { Links = SongLink.FilterSongLinks(songHistory.Song.Links) }
                .DetermineSongStartTimeGetDuration(Quiz.Room.QuizSettings.Filters);

            foreach ((int userId, Dictionary<GuessKind, GuessInfo> guessInfoDict) in songHistory.PlayerGuessInfos)
            {
                // todo? guests
                bool isBot = userId >= Constants.PlayerIdBotMin;
                if (isBot)
                {
                    continue;
                }

                foreach ((GuessKind guessKind, GuessInfo guessInfo) in guessInfoDict)
                {
                    bool shouldAdd = true;
                    switch (guessKind)
                    {
                        case GuessKind.Rigger: // not much point tracking this
                            shouldAdd = false;
                            break;
                        case GuessKind.Developer:
                            if (!hasDeveloper)
                            {
                                shouldAdd = false;
                            }

                            break;
                        case GuessKind.Composer:
                            if (!hasComposer)
                            {
                                shouldAdd = false;
                            }

                            break;
                        case GuessKind.Arranger:
                            if (!hasArranger)
                            {
                                shouldAdd = false;
                            }

                            break;
                        case GuessKind.Lyricist:
                            if (!hasLyricist)
                            {
                                shouldAdd = false;
                            }

                            break;
                        case GuessKind.Mst:
                        case GuessKind.A:
                        case GuessKind.Mt:
                            break;
                        default:
                            throw new ArgumentOutOfRangeException();
                    }

                    if (shouldAdd)
                    {
                        var quizSongHistory = new QuizSongHistory
                        {
                            quiz_id = Quiz.Id,
                            sp = sp,
                            music_id = songHistory.Song.Id,
                            user_id = userId,
                            guess_kind = guessKind,
                            guess = guessInfo.Guess,
                            first_guess_ms = guessInfo.FirstGuessMs,
                            is_correct = guessInfo.IsGuessCorrect,
                            is_on_list = guessInfo.IsOnList,
                            played_at = songHistory.Song.PlayedAt,
                            start_time = startTime,
                            duration = duration,
                        };

                        quizSongHistories.Add(quizSongHistory);
                    }
                }
            }
        }

        try
        {
            // Console.WriteLine(JsonSerializer.Serialize(quizSongHistories, Utils.JsoIndented));
            bool success = await DbManager.InsertEntityBulk(quizSongHistories);
            if (!success)
            {
                throw new Exception();
            }
        }
        catch (Exception e)
        {
            Console.WriteLine("Failed to insert QuizSongHistory");
            Console.WriteLine(e);
        }

        if (Quiz.Room.QuizSettings.ShouldUpdateStats)
        {
            try
            {
                await DbManager.RecalculateSongStats(Quiz.SongsHistory.Select(x => x.Value.Song.Id).ToHashSet());
            }
            catch (Exception e)
            {
                Console.WriteLine("Failed to RecalculateSongStats");
                Console.WriteLine(e);
            }
        }
        else
        {
            Quiz.Room.Log("Not updating stats");
        }
    }

    // todo exclude sources without uploaded bgm for quizzes that have bgm enabled
    // todo generate a and mt from the wrong choices in mst
    // also grab other a and mt from the correct choice

    // todo don't use SelectSongSourceBatch, create new method that returns msid and Title object
    // todo weights, and maybe min/max
    public static async Task<Dictionary<int, List<Title>>> GenerateMultipleChoiceOptions(List<Song> songs,
        List<Session> sessions, QuizSettings quizSettings, TreasureRoom[][] treasureRooms, GuessKind guessKind)
    {
        var stopWatch = new Stopwatch();
        stopWatch.Start();

        var ret = new Dictionary<int, List<Title>>();
        // using SongArtist here is a terrible hack to get Title + Roles without having to create a new type
        Dictionary<int, SongArtist> globalTitles = new();
        HashSet<int> globalAddedIds = new();

        Dictionary<int, Dictionary<MCOptionKind, List<Title>>> validTitlesForSongDict = songs.Select(x => x.Id)
            .ToDictionary(x => x, _ => new Dictionary<MCOptionKind, List<Title>>());
        var connection = new NpgsqlConnection(ConnectionHelper.GetConnectionString());

        int[] labelMids = Array.Empty<int>();
        bool useLists = !quizSettings.Filters.ListReadKindFiltersIsAllRandom && guessKind == GuessKind.Mst;
        if (useLists)
        {
            var allVndbInfos = await ServerUtils.GetAllVndbInfos(sessions);
            labelMids = await DbManager.FindMusicIdsByLabels(allVndbInfos
                .Where(x => x.Labels != null).SelectMany(x => x.Labels!), SongSourceSongTypeMode.Vocals);
        }

        switch (quizSettings.SongSelectionKind)
        {
            case SongSelectionKind.Random:
            case SongSelectionKind.SpacedRepetition:
            case SongSelectionKind.LocalMusicLibrary:
                // we'll want to "top up" with at least one of these two types (Lists, Random) regardless of what other options are enabled
                if (quizSettings.EnabledMCOptionKinds.TryGetValue(MCOptionKind.Lists, out bool l) && l)
                {
                    if (labelMids.Any())
                    {
                        var allPlayerVnTitles = await DbManager.SelectSongsMIds(labelMids, false);
                        foreach (Song song in allPlayerVnTitles)
                        {
                            foreach (SongSource songSource in song.Sources)
                            {
                                if (globalAddedIds.Add(songSource.Id))
                                {
                                    globalTitles.Add(songSource.Id,
                                        new SongArtist()
                                        {
                                            Titles = new List<Title>()
                                            {
                                                Converters.GetSingleTitle(songSource.Titles)
                                            }
                                        });
                                    break;
                                }
                            }
                        }
                    }
                }

                if (!labelMids.Any() ||
                    quizSettings.EnabledMCOptionKinds.TryGetValue(MCOptionKind.Random, out bool r) && r)
                {
                    var selectedMids = songs.Select(x => x.Id).ToHashSet();
                    var randomSongs =
                        await DbManager.GetRandomSongs(songs.Count * 2, false, null, quizSettings.Filters);
                    var randomSongsFiltered = randomSongs.Where(x => !selectedMids.Contains(x.Id)).ToList();

                    foreach (Song song in randomSongsFiltered)
                    {
                        switch (guessKind)
                        {
                            case GuessKind.Mst:
                                foreach (SongSource songSource in song.Sources)
                                {
                                    if (globalAddedIds.Add(songSource.Id))
                                    {
                                        globalTitles.Add(songSource.Id,
                                            new SongArtist()
                                            {
                                                Titles = new List<Title>()
                                                {
                                                    Converters.GetSingleTitle(songSource.Titles)
                                                }
                                            });
                                        break;
                                    }
                                }

                                break;
                            case GuessKind.A:
                                SongArtistRole[] validRoles = song.IsBGM switch
                                {
                                    true => new[] { SongArtistRole.Unknown, SongArtistRole.Composer },
                                    false => new[] { SongArtistRole.Vocals },
                                };

                                foreach (SongArtist songArtist in song.Artists.Where(x =>
                                             validRoles.Any(y => x.Roles.Contains(y))))
                                {
                                    if (globalAddedIds.Add(songArtist.Id))
                                    {
                                        globalTitles.Add(songArtist.Id,
                                            new SongArtist()
                                            {
                                                Titles = new List<Title>()
                                                {
                                                    Converters.GetSingleTitle(songArtist.Titles)
                                                },
                                                Roles = songArtist.Roles,
                                            });
                                        break; // todo? don't stop after first artist?
                                    }
                                }

                                break;
                            case GuessKind.Mt:
                                if (globalAddedIds.Add(song.Id))
                                {
                                    globalTitles.Add(song.Id,
                                        new SongArtist()
                                        {
                                            Titles = new List<Title>() { Converters.GetSingleTitle(song.Titles) }
                                        });
                                }

                                break;
                            case GuessKind.Developer:
                                foreach (SongSourceDeveloper developer in song.Sources.SelectMany(x => x.Developers))
                                {
                                    if (int.TryParse(developer.VndbId.Replace("p", ""), out int id))
                                    {
                                        if (globalAddedIds.Add(id))
                                        {
                                            globalTitles.Add(id,
                                                new SongArtist() { Titles = new List<Title>() { developer.Title } });
                                            break;
                                        }
                                    }
                                }

                                break;
                            case GuessKind.Composer:
                                foreach (SongArtist songArtist in song.Artists.Where(x =>
                                             x.Roles.Contains(SongArtistRole.Composer)))
                                {
                                    if (globalAddedIds.Add(songArtist.Id))
                                    {
                                        globalTitles.Add(songArtist.Id,
                                            new SongArtist()
                                            {
                                                Titles = new List<Title>()
                                                {
                                                    Converters.GetSingleTitle(songArtist.Titles)
                                                },
                                                Roles = songArtist.Roles,
                                            });
                                        break; // todo? don't stop after first artist?
                                    }
                                }

                                break;
                            case GuessKind.Arranger:
                                foreach (SongArtist songArtist in song.Artists.Where(x =>
                                             x.Roles.Contains(SongArtistRole.Arranger)))
                                {
                                    if (globalAddedIds.Add(songArtist.Id))
                                    {
                                        globalTitles.Add(songArtist.Id,
                                            new SongArtist()
                                            {
                                                Titles = new List<Title>()
                                                {
                                                    Converters.GetSingleTitle(songArtist.Titles)
                                                },
                                                Roles = songArtist.Roles,
                                            });
                                        break; // todo? don't stop after first artist?
                                    }
                                }

                                break;
                            case GuessKind.Lyricist:
                                foreach (SongArtist songArtist in song.Artists.Where(x =>
                                             x.Roles.Contains(SongArtistRole.Lyricist)))
                                {
                                    if (globalAddedIds.Add(songArtist.Id))
                                    {
                                        globalTitles.Add(songArtist.Id,
                                            new SongArtist()
                                            {
                                                Titles = new List<Title>()
                                                {
                                                    Converters.GetSingleTitle(songArtist.Titles)
                                                },
                                                Roles = songArtist.Roles,
                                            });
                                        break; // todo? don't stop after first artist?
                                    }
                                }

                                break;
                        }
                    }
                }

                foreach (Song song in songs)
                {
                    if (guessKind != GuessKind.Mst) // todo
                    {
                        continue;
                    }

                    if (quizSettings.EnabledMCOptionKinds.TryGetValue(MCOptionKind.Artist, out bool a) && a)
                    {
                        var msIds = (await connection.QueryAsync<int>(
                            @"SELECT distinct music_source_id
            FROM music_source_music msm
            JOIN artist_music am ON am.music_id = msm.music_id
            WHERE am.artist_id = any(@aIds) and ((@mIds::integer[] IS NULL) or am.music_id = any(@mIds))",
                            new
                            {
                                aIds = song.Artists.Where(x => x.Roles.Any(y => y != SongArtistRole.Lyricist))
                                    .Select(x => x.Id).ToArray(),
                                mIds = labelMids.Any() ? labelMids : null
                            })).ToList();

                        // todo batch
                        var sources = (await DbManager.SelectSongSourceBatch(
                                new NpgsqlConnection(ConnectionHelper.GetConnectionString()),
                                msIds.Select(x =>
                                        new Song { Sources = new List<SongSource> { new() { Id = x } } })
                                    .ToList(), false)).SelectMany(x => x.Value.Select(y => y.Value))
                            .DistinctBy(z => z.Id);

                        validTitlesForSongDict[song.Id][MCOptionKind.Artist] = new List<Title>();
                        foreach (SongSource songSource in sources.OrderBy(d => msIds.IndexOf(d.Id)))
                        {
                            // todo msId dupe check?
                            validTitlesForSongDict[song.Id][MCOptionKind.Artist]
                                .Add(Converters.GetSingleTitle(songSource.Titles));
                        }
                    }

                    if (quizSettings.EnabledMCOptionKinds.TryGetValue(MCOptionKind.ArtistPair, out bool ap) && ap)
                    {
                        // todo batch
                        var collabMids = ArtistCollaborationFinder.FindPairwiseCollaborations(song.Artists
                            .Where(x => x.Roles.Any(y => y != SongArtistRole.Lyricist)).Select(x => x.Id)
                            .ToArray()).ToArray();

                        var msIds = (await connection.QueryAsync<int>(
                                "select distinct music_source_id from music_source_music where music_id = any(@mIds)",
                                new
                                {
                                    mIds = labelMids.Any() ? collabMids.Intersect(labelMids).ToArray() : collabMids
                                }))
                            .ToList();

                        // todo batch
                        var sources = (await DbManager.SelectSongSourceBatch(
                                new NpgsqlConnection(ConnectionHelper.GetConnectionString()),
                                msIds.Select(x =>
                                        new Song { Sources = new List<SongSource> { new() { Id = x } } })
                                    .ToList(), false)).SelectMany(x => x.Value.Select(y => y.Value))
                            .DistinctBy(z => z.Id);

                        validTitlesForSongDict[song.Id][MCOptionKind.ArtistPair] = new List<Title>();
                        foreach (SongSource songSource in sources.OrderBy(d => msIds.IndexOf(d.Id)))
                        {
                            // todo msId dupe check?
                            validTitlesForSongDict[song.Id][MCOptionKind.ArtistPair]
                                .Add(Converters.GetSingleTitle(songSource.Titles));
                        }
                    }

                    if (quizSettings.EnabledMCOptionKinds.TryGetValue(MCOptionKind.Developer, out bool d) && d)
                    {
                        throw new NotImplementedException();
                    }

                    if (quizSettings.EnabledMCOptionKinds.TryGetValue(MCOptionKind.Qsh, out bool qsh) && qsh)
                    {
                        if (DbManager.McOptionsQshDict.TryGetValue(song.Id, out var msIds))
                        {
                            // todo batch
                            var sources = (await DbManager.SelectSongSourceBatch(connection,
                                    msIds.Select(x =>
                                            new Song { Sources = new List<SongSource> { new() { Id = x } } })
                                        .ToList(), false))
                                .Where(x => !labelMids.Any() || labelMids.Contains(x.Key))
                                .SelectMany(x => x.Value.Select(y => y.Value))
                                .DistinctBy(z => z.Id);

                            validTitlesForSongDict[song.Id][MCOptionKind.Qsh] = new List<Title>();
                            foreach (SongSource songSource in sources.OrderBy(x => msIds.IndexOf(x.Id)))
                            {
                                // todo msId dupe check?
                                validTitlesForSongDict[song.Id][MCOptionKind.Qsh]
                                    .Add(Converters.GetSingleTitle(songSource.Titles));
                            }
                        }
                    }
                }

                break;
            case SongSelectionKind.Looting:
                if (guessKind != GuessKind.Mst)
                {
                    throw new NotImplementedException();
                }

                // generate wrong multiple choice options from the VNs on the ground while looting
                List<KeyValuePair<string, List<Title>>> validSources = treasureRooms
                    .SelectMany(x => x.SelectMany(y => y.Treasures.Select(z => z.ValidSource))).ToList();

                foreach (Session session in sessions)
                {
                    foreach (var treasure in session.Player.LootingInfo.Inventory)
                    {
                        validSources.Add(treasure.ValidSource);
                    }
                }

                validSources = validSources.DistinctBy(x => x.Key).ToList();
                foreach ((string key, List<Title> value) in validSources)
                {
                    if (globalAddedIds.Add(key.GetHashCode()))
                    {
                        globalTitles.Add(key.GetHashCode(),
                            new SongArtist() { Titles = new List<Title>() { Converters.GetSingleTitle(value) } });
                    }
                }

                break;
            default:
                throw new ArgumentOutOfRangeException();
        }

        // Console.WriteLine(JsonSerializer.Serialize(addedSourceIds, Utils.Jso));
        // Console.WriteLine(JsonSerializer.Serialize(allTitles, Utils.Jso));

        List<string> seenCorrectAnswerLatinTitles = new();
        for (int index = 0; index < songs.Count; index++)
        {
            Song dbSong = songs[index];
            Title correctAnswerTitle = guessKind switch
            {
                GuessKind.Mst => Converters.GetSingleTitle(dbSong.Sources.First().Titles),
                GuessKind.A =>
                    Converters.GetSingleTitle(dbSong.Artists.First(x =>
                        (!dbSong.IsBGM && x.Roles.Contains(SongArtistRole.Vocals)) ||
                        dbSong.IsBGM && (x.Roles.Contains(SongArtistRole.Unknown) ||
                                         x.Roles.Contains(SongArtistRole.Composer))).Titles),
                GuessKind.Mt => Converters.GetSingleTitle(dbSong.Titles),
                GuessKind.Developer => dbSong.Sources.First().Developers.FirstOrDefault()?.Title ?? new Title(),
                GuessKind.Composer => Converters.GetSingleTitle(
                    dbSong.Artists.FirstOrDefault(x => x.Roles.Contains(SongArtistRole.Composer))?.Titles ??
                    new List<Title>() { new() }),
                GuessKind.Arranger => Converters.GetSingleTitle(
                    dbSong.Artists.FirstOrDefault(x => x.Roles.Contains(SongArtistRole.Arranger))?.Titles ??
                    new List<Title>() { new() }),
                GuessKind.Lyricist => Converters.GetSingleTitle(
                    dbSong.Artists.FirstOrDefault(x => x.Roles.Contains(SongArtistRole.Lyricist))?.Titles ??
                    new List<Title>() { new() }),
                _ => new Title()
            };

            seenCorrectAnswerLatinTitles.Add(correctAnswerTitle.LatinTitle);
            List<Title> list = new() { correctAnswerTitle };

            // process the advanced option kinds first
            if (validTitlesForSongDict.TryGetValue(dbSong.Id, out var dicts))
            {
                foreach ((MCOptionKind key, List<Title>? value) in dicts.OrderByDescending(x => x.Key)) // todo weights
                {
                    if (list.Count >= quizSettings.NumMultipleChoiceOptions)
                    {
                        break;
                    }

                    var titles = key == MCOptionKind.Qsh
                        ? value.DistinctBy(x => x.LatinTitle) // order is important for this one
                        : value.DistinctBy(x => x.LatinTitle).Shuffle();
                    foreach (Title title in titles)
                    {
                        if (list.Count >= quizSettings.NumMultipleChoiceOptions)
                        {
                            break;
                        }

                        if (quizSettings.Duplicates || !seenCorrectAnswerLatinTitles.Contains(title.LatinTitle))
                        {
                            if (!list.Any(x => x.LatinTitle == title.LatinTitle))
                            {
                                list.Add(title);
                            }
                        }
                    }
                }
            }

            // then top-up
            if (list.Count < quizSettings.NumMultipleChoiceOptions)
            {
                foreach ((int _, SongArtist a) in globalTitles.Shuffle())
                {
                    if (list.Count >= quizSettings.NumMultipleChoiceOptions)
                    {
                        break;
                    }

                    switch (guessKind)
                    {
                        case GuessKind.A:
                            if (dbSong.IsBGM)
                            {
                                if (!a.Roles.Contains(SongArtistRole.Unknown) &&
                                    !a.Roles.Contains(SongArtistRole.Composer))
                                {
                                    continue;
                                }
                            }
                            else
                            {
                                if (!a.Roles.Contains(SongArtistRole.Vocals))
                                {
                                    continue;
                                }
                            }

                            break;
                        case GuessKind.Composer:
                            if (!a.Roles.Contains(SongArtistRole.Composer))
                            {
                                continue;
                            }

                            break;
                        case GuessKind.Arranger:
                            if (!a.Roles.Contains(SongArtistRole.Arranger))
                            {
                                continue;
                            }

                            break;
                        case GuessKind.Lyricist:
                            if (!a.Roles.Contains(SongArtistRole.Lyricist))
                            {
                                continue;
                            }

                            break;
                    }

                    var title = a.Titles.Single();
                    if (quizSettings.Duplicates || !seenCorrectAnswerLatinTitles.Contains(title.LatinTitle))
                    {
                        if (!list.Any(x => x.LatinTitle == title.LatinTitle))
                        {
                            list.Add(title);
                        }
                    }
                }
            }

            list = list.DistinctBy(x => x.LatinTitle).Shuffle().ToList();
            ret[index] = list;
        }

        stopWatch.Stop();
        Console.WriteLine(
            $"{nameof(GenerateMultipleChoiceOptions)} took {Math.Round(((stopWatch.ElapsedTicks * 1000.0) / Stopwatch.Frequency) / 1000, 2)}s");

        return ret;
    }

    private async Task EnterQuiz()
    {
        // we have to do these here instead of PrimeQuiz because songs won't be determined until here if it's looting
        switch (Quiz.Room.QuizSettings.AnsweringKind)
        {
            case AnsweringKind.Typing:
                Quiz.MultipleChoiceOptions.Clear();
                break;
            case AnsweringKind.MultipleChoice:
            case AnsweringKind.Mixed:
                var playerSessions =
                    ServerState.Sessions.Where(x => Quiz.Room.Players.Any(y => y.Id == x.Player.Id)).ToList();
                foreach ((GuessKind key, bool value) in Quiz.Room.QuizSettings.EnabledGuessKinds)
                {
                    if (value)
                    {
                        Quiz.MultipleChoiceOptions[key] = await GenerateMultipleChoiceOptions(Quiz.Songs,
                            playerSessions, Quiz.Room.QuizSettings, Quiz.Room.TreasureRooms, key);
                    }
                }

                break;
            default:
                throw new ArgumentOutOfRangeException();
        }

        if (Quiz.Room.QuizSettings.IsMergeArtistAliases &&
            ((Quiz.Room.QuizSettings.EnabledGuessKinds.TryGetValue(GuessKind.A, out bool a) && a) ||
             (Quiz.Room.QuizSettings.EnabledGuessKinds.TryGetValue(GuessKind.Composer, out bool c) && c) ||
             (Quiz.Room.QuizSettings.EnabledGuessKinds.TryGetValue(GuessKind.Arranger, out bool arr) && arr) ||
             (Quiz.Room.QuizSettings.EnabledGuessKinds.TryGetValue(GuessKind.Lyricist, out bool l) && l)))
        {
            ArtistAliasesDict =
                await DbManager.SelectArtistAliases(Quiz.Songs.SelectMany(x => x.Artists.Select(y => y.Id)).ToArray());

            if (Quiz.Room.QuizSettings.IsMergeArtistBands)
            {
                var artistArtists = Quiz.Songs
                    .SelectMany(x => x.Artists.SelectMany(y =>
                        y.ArtistArtists.Where(z => z.rel == ArtistArtistRelKind.MemberOfBand))).ToArray();
                var artistAliasesDict =
                    await DbManager.SelectArtistAliases(artistArtists.Select(x => x.source).ToArray());

                var ret = new Dictionary<int, List<string>>();
                foreach (ArtistArtist arar in artistArtists)
                {
                    if (!ret.TryGetValue(arar.target, out var list))
                    {
                        list = new List<string>();
                    }

                    list.AddRange(artistAliasesDict[arar.source]);
                    ret[arar.target] = list;
                }

                ArtistBandsDict = ret.ToFrozenDictionary(x => x.Key, x => x.Value.ToArray());
            }
        }

        // reduce serialized Room size
        Quiz.Room.TreasureRooms = Array.Empty<TreasureRoom[]>();

        if (!Quiz.Room.QuizSettings.AllowViewingInventoryDuringQuiz)
        {
            foreach (var player in Quiz.Room.Players)
            {
                // reduce serialized Room size & prevent Inventory leak
                player.LootingInfo = new PlayerLootingInfo();
            }
        }

        var mimics = Quiz.Room.Players.Where(x => x.BotInfo is { BotKind: PlayerBotKind.Mimic }).ToArray();
        if (mimics.Any())
        {
            await using var connectionAuth = new NpgsqlConnection(ConnectionHelper.GetConnectionString_Auth());
            var usernamesDict =
                (await connectionAuth.QueryAsync<(int, string)>(
                    "select id, username from users where lower(username) = ANY(lower(@usernames::text)::text[])",
                    new { usernames = mimics.Select(x => x.BotInfo!.MimickedUsername).ToArray() }))
                .ToDictionary(x => x.Item1, x => x.Item2); // todo important cache this

            var userSongStatsLookup =
                await DbManager.GetSHPlayerSongStats(Quiz.Songs.Select(x => x.Id).ToList(),
                    usernamesDict.Select(x => x.Key).ToList());

            foreach (Player mimic in mimics)
            {
                int mimickedUserId = usernamesDict.FirstOrDefault(x => string.Equals(x.Value,
                    mimic.BotInfo!.MimickedUsername,
                    StringComparison.InvariantCultureIgnoreCase)).Key;
                if (mimickedUserId > 0)
                {
                    var userSongStats = userSongStatsLookup[mimickedUserId].ToArray();
                    foreach (Song song in Quiz.Songs)
                    {
                        mimic.BotInfo!.SongHitChanceDict[song.Id] = new Dictionary<GuessKind, float>();

                        var stats = userSongStats.FirstOrDefault(x =>
                            x.Values.Any(y => y.Values.Any(z => z.MusicId == song.Id)));
                        if (stats != null)
                        {
                            foreach ((GuessKind key, var value) in stats)
                            {
                                mimic.BotInfo.SongHitChanceDict[song.Id]![key] =
                                    value.GetValueOrDefault(song.Id)?.CorrectPercentage ?? 0;
                            }
                        }
                    }
                }
            }
        }

        await using var connection = new NpgsqlConnection(ConnectionHelper.GetConnectionString());
        var votes = (await connection.QueryAsync<MusicVote>(
            @"SELECT * FROM music_vote WHERE user_id = ANY(@uIds) AND music_id = ANY(@mIds)",
            new
            {
                uIds = Quiz.Room.Players.Select(x => x.Id).ToArray(), mIds = Quiz.Songs.Select(x => x.Id).ToArray()
            })).ToArray();

        foreach (var song in Quiz.Songs)
        {
            song.PlayerVotes = votes.Where(x => x.music_id == song.Id).ToDictionary(x => x.user_id, x => x.vote!.Value);
        }

        TypedQuizHub.ReceiveQuizEntered(Quiz.Room.Players.Concat(Quiz.Room.Spectators).Select(x => x.Id));
        await Task.Delay(TimeSpan.FromSeconds(1));
    }

    // todo Exclude does nothing on its own
    public async Task<bool> PrimeQuiz()
    {
        var teams = Quiz.Room.Players.GroupBy(x => x.TeamId).ToList();
        if (Quiz.Room.QuizSettings.TeamSize > 1)
        {
            var fullTeam = teams.FirstOrDefault(x => x.Count() > Quiz.Room.QuizSettings.TeamSize);
            if (fullTeam != null)
            {
                Quiz.Room.Log($"Team {fullTeam.Key} has too many players.", writeToChat: true);
                return false;
            }

            // todo? figure out how to handle hotjoining players for non-one-team teamed games
            if (teams.Count > 1 || teams.Single().First().TeamId != 1)
            {
                Quiz.Room.QuizSettings.IsHotjoinEnabled = false;
            }
        }

        if (Quiz.Room.QuizSettings.GamemodeKind is GamemodeKind.NGMC or GamemodeKind.EruMode)
        {
            if (teams.Count < 2)
            {
                Quiz.Room.Log($"Gamemode: There must be at least two teams.", writeToChat: true);
                return false;
            }

            if (Quiz.Room.QuizSettings.GamemodeKind is GamemodeKind.NGMC &&
                Quiz.Room.Players.Any(x => x.TeamId is < 1 or > 2))
            {
                Quiz.Room.Log($"Gamemode: The teams must use the team ids 1 and 2.", writeToChat: true);
                return false;
            }

            int highestTeamIdSeen = 0;
            foreach (Player player in Quiz.Room.Players)
            {
                if (player.TeamId > highestTeamIdSeen)
                {
                    highestTeamIdSeen = player.TeamId;
                }
                else if (player.TeamId < highestTeamIdSeen)
                {
                    Quiz.Room.Log($"Gamemode: The teams must be in sequential order.", writeToChat: true);
                    return false;
                }
            }

            if (Quiz.Room.QuizSettings.MaxLives < 1)
            {
                Quiz.Room.Log($"Gamemode: The Lives setting must be greater than 0.", writeToChat: true);
                return false;
            }

            if (Quiz.Room.QuizSettings.GamemodeKind == GamemodeKind.NGMC)
            {
                if (Quiz.Room.Players.Any(x => x.NGMCGuessesInitial < 1))
                {
                    Quiz.Room.Log($"NGMC: Every player must have at least 1 guess.", writeToChat: true);
                    return false;
                }
            }

            Quiz.Room.QuizSettings.IsHotjoinEnabled = false;
        }

        if (!Quiz.Room.QuizSettings.EnabledGuessKinds.Any(x => x.Value))
        {
            Quiz.Room.Log("At least one Guess type must be enabled.", writeToChat: true);
            return false;
        }

        if (!Quiz.Room.QuizSettings.IsOnlyMstGuessTypeEnabled)
        {
            if (Quiz.Room.QuizSettings.SongSelectionKind == SongSelectionKind.Looting &&
                Quiz.Room.QuizSettings.AnsweringKind is AnsweringKind.MultipleChoice or AnsweringKind.Mixed)
            {
                Quiz.Room.Log(
                    $"The {SongSelectionKind.Looting} Song selection method only supports the \"{GuessKind.Mst.GetDescription()}\" Guess type for the \"{AnsweringKind.MultipleChoice.GetDescription()}\" Answering method.",
                    writeToChat: true);
                return false;
            }
        }

        if (Quiz.Room.QuizSettings.IsNoSoundMode)
        {
            Quiz.Room.QuizSettings.TimeoutMs = 5000;
        }

        if (Quiz.Room.QuizSettings.ListDistributionKind == ListDistributionKind.Balanced)
        {
            if (Quiz.Room.QuizSettings.Filters.ListReadKindFiltersHasUnread)
            {
                Quiz.Room.QuizSettings.ListDistributionKind = ListDistributionKind.Random;
            }
        }

        int playersInActiveQuizzes = ServerState.Rooms.Where(x =>
                x.Quiz != null && !x.Quiz.QuizState.IsPaused && x.Quiz.QuizState.QuizStatus == QuizStatus.Playing)
            .Sum(x => x.Players.Count(y => !y.IsBot));
        if (Quiz.Room.Players.Count(x => !x.IsBot) > 1)
        {
            Quiz.Room.QuizSettings.TimeoutMs = playersInActiveQuizzes switch
            {
                <= 10 => Quiz.Room.QuizSettings.TimeoutMs,
                > 10 and <= 15 => Math.Max(6000, Quiz.Room.QuizSettings.TimeoutMs),
                > 15 and <= 20 => Math.Max(7000, Quiz.Room.QuizSettings.TimeoutMs),
                > 20 and <= 25 => Math.Max(9000, Quiz.Room.QuizSettings.TimeoutMs),
                > 25 and <= 30 => Math.Max(10000, Quiz.Room.QuizSettings.TimeoutMs),
                > 30 and <= 40 => Math.Max(12000, Quiz.Room.QuizSettings.TimeoutMs),
                > 40 and <= 50 => Math.Max(14000, Quiz.Room.QuizSettings.TimeoutMs),
                > 50 and <= 70 => Math.Max(17000, Quiz.Room.QuizSettings.TimeoutMs),
                > 70 and <= 99 => Math.Max(20000, Quiz.Room.QuizSettings.TimeoutMs),
                > 99 => Math.Max(25000, Quiz.Room.QuizSettings.TimeoutMs),
            };
        }

        CorrectAnswersDicts =
            Enum.GetValues<GuessKind>().ToDictionary(x => x, _ => new Dictionary<int, List<string>>());
        ArtistAliasesDict = null;
        ArtistBandsDict = null;
        Dictionary<int, List<string>> validSourcesDict = new();

        var playerSessions = ServerState.Sessions.Where(x => Quiz.Room.Players.Any(y => y.Id == x.Player.Id))
            .ToDictionary(x => x.Player.Id, x => x);
        foreach (Player player in Quiz.Room.Players)
        {
            player.Lives = Quiz.Room.QuizSettings.MaxLives;
            player.Score = 0;
            player.AnsweringKind = Quiz.Room.QuizSettings.AnsweringKind == AnsweringKind.Mixed
                ? player.AnsweringKind
                : Quiz.Room.QuizSettings.AnsweringKind;
            player.Guess = null;
            player.IsGuessKindCorrectDict = null;
            player.IsBuffered = false;
            player.IsSkipping = false;
            // do not set player.IsReadiedUp to false here, because it would be annoying to ready up again if we return false
            player.PlayerStatus = PlayerStatus.Default;
            player.LootingInfo = new PlayerLootingInfo();
            player.NGMCGuessesCurrent = player.NGMCGuessesInitial;
            player.NGMCCanBurn = false;
            player.NGMCMustPick = false;
            player.NGMCCanBePicked = false;
            player.NGMCMustBurn = false;
            player.BotInfo?.SongHitChanceDict.Clear();

            if (!Quiz.Room.QuizSettings.Filters.ListReadKindFiltersIsAllRandom)
            {
                if (!playerSessions.TryGetValue(player.Id, out Session? session)) // Bot player
                {
                    continue;
                }

                var vndbInfo = await ServerUtils.GetVndbInfo_Inner(player.Id, session.ActiveUserLabelPresetName);
                if (string.IsNullOrWhiteSpace(vndbInfo.VndbId) ||
                    string.IsNullOrEmpty(session.ActiveUserLabelPresetName))
                {
                    continue;
                }

                if (vndbInfo.Labels != null)
                {
                    var userLabels =
                        await DbManager_Auth.GetUserLabels(player.Id, vndbInfo.VndbId,
                            session.ActiveUserLabelPresetName);
                    var include = userLabels.Where(x => x.kind == LabelKind.Include).ToList();
                    var exclude = userLabels.Where(x => x.kind == LabelKind.Exclude).ToList();

                    // todo Exclude does nothing on its own (don't break Balanced while fixing this)
                    if (include.Any())
                    {
                        validSourcesDict[player.Id] =
                            (await DbManager_Auth.GetUserLabelVns(include.Select(x => x.id).ToList()))
                            .Select(x => x.vnid).ToList();

                        if (exclude.Any())
                        {
                            List<string> excluded =
                                (await DbManager_Auth.GetUserLabelVns(exclude.Select(x => x.id).ToList()))
                                .Select(x => x.vnid).ToList();

                            validSourcesDict[player.Id] = validSourcesDict[player.Id].Except(excluded).ToList();
                        }

                        validSourcesDict[player.Id] = validSourcesDict[player.Id].Distinct().ToList();
                    }
                }
            }
        }

        Quiz.Room.QuizSettings.Filters.VndbAdvsearchFilter =
            Quiz.Room.QuizSettings.Filters.VndbAdvsearchFilter.SanitizeVndbAdvsearchStr();
        if (!string.IsNullOrWhiteSpace(Quiz.Room.QuizSettings.Filters.VndbAdvsearchFilter))
        {
            Quiz.Room.Log($"VNDB search filter is being processed.", -1, true);
            bool success = false;
            var cancellationTokenSource = new CancellationTokenSource();
            cancellationTokenSource.CancelAfter(TimeSpan.FromSeconds(5));

            var task = Task.Run(async () =>
            {
                string[]? vndbUrls =
                    await VndbMethods.GetVnUrlsMatchingAdvsearchStr(null,
                        Quiz.Room.QuizSettings.Filters.VndbAdvsearchFilter, cancellationTokenSource.Token);

                if (vndbUrls == null || !vndbUrls.Any())
                {
                    Quiz.Room.Log($"VNDB search filter returned no results.", -1, true);
                    success = false;
                    return;
                }

                validSourcesDict.Clear();
                validSourcesDict[-1] = vndbUrls.Distinct().ToList();
                Quiz.Room.Log($"VNDB search filter returned {validSourcesDict.Single().Value.Count} results.", -1,
                    true);
                Quiz.Room.Log("validSources overridden by VndbAdvsearchFilter: " +
                              JsonSerializer.Serialize(validSourcesDict, Utils.Jso));

                success = true;
            }, cancellationTokenSource.Token);

            try
            {
                await task;
            }
            catch (Exception)
            {
                Quiz.Room.Log($"VNDB search took longer than 5 seconds - canceling.", -1, true);
            }

            if (!success)
            {
                return false;
            }
        }
        else
        {
            Quiz.Room.Log("validSources: " + JsonSerializer.Serialize(validSourcesDict, Utils.Jso),
                writeToConsole: false);
        }

        // Quiz.Room.Log($"validSourcesCount: {validSources.Count}");

        // var validCategories = Quiz.Room.QuizSettings.Filters.CategoryFilters;
        // Quiz.Room.Log("validCategories: " + JsonSerializer.Serialize(validCategories, Utils.Jso));
        // Quiz.Room.Log($"validCategoriesCount: {validCategories.Count}");

        // var validArtists = Quiz.Room.QuizSettings.Filters.ArtistFilters;
        // Quiz.Room.Log("validArtists: " + JsonSerializer.Serialize(validArtists, Utils.Jso));
        // Quiz.Room.Log($"validArtistsCount: {validArtists.Count}");

        // todo handle hotjoining players
        var vndbInfos = new Dictionary<int, PlayerVndbInfo>();
        foreach ((int _, Session session) in playerSessions)
        {
            vndbInfos[session.Player.Id] =
                await ServerUtils.GetVndbInfo_Inner(session.Player.Id, session.ActiveUserLabelPresetName);
        }

        List<Song> dbSongs;
        switch (Quiz.Room.QuizSettings.SongSelectionKind)
        {
            case SongSelectionKind.Random:
            case SongSelectionKind.SpacedRepetition:
                List<int>? validMids = null;
                List<int>? invalidMids = null;
                if (Quiz.Room.QuizSettings.SongSelectionKind == SongSelectionKind.SpacedRepetition)
                {
                    switch (Quiz.Room.QuizSettings.SpacedRepetitionKind)
                    {
                        case SpacedRepetitionKind.Review:
                            validMids =
                                await DbManager.GetMidsWithReviewsDue(Quiz.Room.Players.Concat(Quiz.Room.Spectators)
                                    .Select(x => x.Id).ToList());
                            Quiz.Room.Log($"{validMids.Count} songs are due for review.", writeToChat: true);
                            break;
                        case SpacedRepetitionKind.NoIntervalOnly:
                            invalidMids =
                                await DbManager.GetMidsWithIntervals(Quiz.Room.Players.Concat(Quiz.Room.Spectators)
                                    .Select(x => x.Id).ToList());
                            Quiz.Room.Log($"Excluding {invalidMids.Count} songs with intervals.", writeToChat: true);
                            break;
                        default:
                            throw new ArgumentOutOfRangeException();
                    }
                }

                switch (Quiz.Room.QuizSettings.ListDistributionKind)
                {
                    case ListDistributionKind.Random:
                    case ListDistributionKind.CappedRandom:
                        {
                            if (Quiz.Room.QuizSettings.ListDistributionKind == ListDistributionKind.CappedRandom)
                            {
                                int n = Quiz.Room.QuizSettings.CappedRandomLimit;
                                foreach ((int key, List<string> value) in validSourcesDict)
                                {
                                    if (value.Count > n)
                                    {
                                        validSourcesDict[key] = value.Shuffle().Take(n).ToList();
                                    }
                                }
                            }

                            dbSongs = await DbManager.GetRandomSongs(Quiz.Room.QuizSettings.NumSongs,
                                Quiz.Room.QuizSettings.Duplicates,
                                validSourcesDict.SelectMany(x => x.Value).Distinct().ToList(),
                                filters: Quiz.Room.QuizSettings.Filters, players: Quiz.Room.Players.ToList(),
                                listDistributionKind: Quiz.Room.QuizSettings.ListDistributionKind,
                                validMids: validMids, invalidMids: invalidMids, ownerUserId: Quiz.Room.Owner.Id,
                                gamemodeKind: Quiz.Room.QuizSettings.GamemodeKind);
                            break;
                        }
                    case ListDistributionKind.Balanced:
                        {
                            // todo tests
                            if (validSourcesDict.Count < 2)
                            {
                                Quiz.Room.Log(
                                    $"Balanced mode requires there to be at least two players with a list active; falling back to {ListDistributionKind.Random.ToString()}.",
                                    writeToChat: true);
                                // #BlameAkaze
                                goto case ListDistributionKind.Random;
                            }

                            var songTypesLeft = Quiz.Room.QuizSettings.Filters.SongSourceSongTypeFilters
                                .OrderByDescending(x => x.Key) // Random must be selected first
                                .Where(x => x.Value.Value > 0)
                                .ToDictionary(x => x.Key, x => x.Value.Value);

                            var listReadKindLeft = Quiz.Room.QuizSettings.Filters.ListReadKindFilters
                                .OrderByDescending(x => x.Key) // Random must be selected first
                                .Where(x => x.Value.Value > 0)
                                .ToDictionary(x => x.Key, x => x.Value.Value);

                            int readCount = Quiz.Room.QuizSettings.Filters.ListReadKindFilters
                                .GetValueOrDefault(ListReadKind.Read)?.Value ?? 0;

                            int targetNumSongsPerPlayer = Math.Min(
                                readCount / validSourcesDict.Count,
                                validSourcesDict.MinBy(x => x.Value.Count).Value.Count);
                            Console.WriteLine($"targetNumSongsPerPlayer: {targetNumSongsPerPlayer}");

                            dbSongs = new List<Song>();
                            invalidMids ??= new List<int>();

                            // Select Random ListReadKind songs first
                            if (listReadKindLeft.TryGetValue(ListReadKind.Random, out int r) && r > 0)
                            {
                                dbSongs.AddRange(await DbManager.GetRandomSongs(
                                    Quiz.Room.QuizSettings.NumSongs - readCount,
                                    Quiz.Room.QuizSettings.Duplicates,
                                    null,
                                    filters: Quiz.Room.QuizSettings.Filters, players: Quiz.Room.Players.ToList(),
                                    validMids: validMids, invalidMids: invalidMids, songTypesLeft: songTypesLeft,
                                    ownerUserId: Quiz.Room.Owner.Id,
                                    gamemodeKind: Quiz.Room.QuizSettings.GamemodeKind,
                                    listReadKindLeft: listReadKindLeft));
                                invalidMids.AddRange(dbSongs.Select(x => x.Id));
                            }

                            // then select the Read songs.
                            // We randomize the players here in order to make sure that the first player doesn't get all the EDs (etc.) if EDs are set to a low amount.
                            foreach ((int pId, _) in validSourcesDict.Shuffle())
                            {
                                var player = Quiz.Room.Players.Single(x => x.Id == pId);
                                Console.WriteLine(
                                    $"selecting {targetNumSongsPerPlayer} songs for p{pId} {player.Username}");

                                if (Quiz.Room.QuizSettings.IsPreventSameSongSpam)
                                {
                                    var playedInTheLastXMinutes = player.SongLastPlayedAtDict.Where(x =>
                                            (DateTime.UtcNow - x.Value) <
                                            TimeSpan.FromMinutes(Quiz.Room.QuizSettings.PreventSameSongSpamMinutes))
                                        .Select(x => x.Key);
                                    invalidMids.AddRange(playedInTheLastXMinutes);
                                }

                                if (Quiz.Room.QuizSettings.IsPreventSameVNSpam)
                                {
                                    var playedInTheLastXMinutes = player.VNLastPlayedAtDict.Where(x =>
                                            (DateTime.UtcNow - x.Value) <
                                            TimeSpan.FromMinutes(Quiz.Room.QuizSettings.PreventSameVNSpamMinutes))
                                        .Select(x => x.Key);
                                    validSourcesDict[pId].RemoveAll(x => playedInTheLastXMinutes.Contains(x));
                                }

                                dbSongs.AddRange(await DbManager.GetRandomSongs(
                                    targetNumSongsPerPlayer,
                                    Quiz.Room.QuizSettings.Duplicates,
                                    validSourcesDict[pId].Shuffle().ToList(),
                                    filters: Quiz.Room.QuizSettings.Filters, players: Quiz.Room.Players.ToList(),
                                    validMids: validMids, invalidMids: invalidMids, songTypesLeft: songTypesLeft,
                                    ownerUserId: Quiz.Room.Owner.Id,
                                    gamemodeKind: Quiz.Room.QuizSettings.GamemodeKind,
                                    listReadKindLeft: listReadKindLeft));
                                invalidMids.AddRange(dbSongs.Select(x => x.Id));
                            }

                            if (!Quiz.Room.QuizSettings.Duplicates)
                            {
                                Console.WriteLine($"dbSongs.Count before distinct1: {dbSongs.Count}");
                                var finalDbSongs = new List<Song>();
                                var seenSourceIds = new HashSet<int>();
                                foreach (Song dbSong in dbSongs)
                                {
                                    foreach (SongSource dbSongSource in dbSong.Sources)
                                    {
                                        if (seenSourceIds.Add(dbSongSource.Id))
                                        {
                                            finalDbSongs.Add(dbSong);
                                        }
                                    }
                                }

                                dbSongs = finalDbSongs;
                                Console.WriteLine($"dbSongs.Count after distinct1: {dbSongs.Count}");
                            }

                            Console.WriteLine($"dbSongs.Count before distinct2: {dbSongs.Count}");
                            dbSongs = dbSongs.DistinctBy(x => x.Id).ToList();
                            Console.WriteLine($"dbSongs.Count after distinct2: {dbSongs.Count}");

                            // int diff = (targetNumSongsPerPlayer * validSourcesDict.Count) - dbSongs.Count;
                            // Console.WriteLine($"NumSongs to actual diff: {diff}");

                            Quiz.Room.Log($"Balanced mode tried to select {targetNumSongsPerPlayer} songs per player.",
                                writeToChat: true);

                            dbSongs = dbSongs.Shuffle().ToList();
                            break;
                        }
                    default:
                        throw new ArgumentOutOfRangeException();
                }

                if (dbSongs.Count == 0)
                {
                    return false;
                }

                foreach (Song dbSong in dbSongs)
                {
                    dbSong.PlayerLabels = GetPlayerLabelsForSong(dbSong, vndbInfos);
                }

                Quiz.Songs = dbSongs;
                Quiz.QuizState.NumSongs = Quiz.Songs.Count;
                break;
            case SongSelectionKind.Looting:
                dbSongs = new List<Song>();
                var cancellationTokenSource = new CancellationTokenSource();
                cancellationTokenSource.CancelAfter(TimeSpan.FromSeconds(5));

                // todo lots of selects are performed when NumSongs is really small
                int songsLeft =
                    Math.Max(
                        (int)((Quiz.Room.QuizSettings.NumSongs / 1.5f) * (((float)Quiz.Room.Players.Count + 3) / 2)),
                        120);
                while (songsLeft > 0 && !cancellationTokenSource.IsCancellationRequested)
                {
                    var selectedSongs = await DbManager.GetRandomSongs(songsLeft,
                        Quiz.Room.QuizSettings.Duplicates,
                        validSourcesDict.SelectMany(x => x.Value).Distinct().ToList(),
                        filters: Quiz.Room.QuizSettings.Filters, players: Quiz.Room.Players.ToList(),
                        ownerUserId: Quiz.Room.Owner.Id, gamemodeKind: Quiz.Room.QuizSettings.GamemodeKind,
                        songSelectionKind: Quiz.Room.QuizSettings.SongSelectionKind);

                    if (!selectedSongs.Any())
                    {
                        break;
                    }

                    songsLeft -= selectedSongs.Count;
                    dbSongs.AddRange(selectedSongs);
                }

                Console.WriteLine($"Looting dbSongs.Count: {dbSongs.Count}");
                dbSongs = dbSongs.DistinctBy(x => x.Id).ToList();
                Console.WriteLine($"Looting dbSongs.Count distinct: {dbSongs.Count}");

                if (dbSongs.Count == 0)
                {
                    return false;
                }

                var validSourcesLooting = new Dictionary<string, List<Title>>();
                foreach (Song dbSong in dbSongs)
                {
                    foreach (var dbSongSource in dbSong.Sources)
                    {
                        // todo songs with multiple vns overriding each other
                        validSourcesLooting[
                                (dbSongSource.Links.FirstOrDefault(x => x.Type == SongSourceLinkType.VNDB) ??
                                 dbSongSource.Links.First()).Url] =
                            dbSongSource.Titles;
                    }
                }

                Quiz.ValidSourcesForLooting = validSourcesLooting;
                break;
            case SongSelectionKind.LocalMusicLibrary:
                bool b = false;
                if (!b)
                {
                    throw new Exception("broken since who knows when");
                }

                dbSongs = new List<Song>();
                string[] filePaths =
                    Directory.GetFiles(Constants.LocalMusicLibraryPath, "*.mp3", SearchOption.AllDirectories);
                for (int i = 0; i < Quiz.Room.QuizSettings.NumSongs; i++)
                {
                    var song = new Song() { Id = i };
                    dbSongs.Add(song);

                    string filePath = filePaths[Random.Shared.Next(filePaths.Length - 1)];
                    try
                    {
                        var tFile = TagLib.File.Create(filePath);
                        string? metadataSources = tFile.Tag.Album;
                        string? metadataTitle = tFile.Tag.Title;
                        string[] metadataArtists = tFile.Tag.Performers.Concat(tFile.Tag.AlbumArtists).ToArray();
                        if (!metadataArtists.Any())
                        {
                            metadataArtists = new[] { "" };
                        }

                        song.Sources.Add(new SongSource()
                        {
                            Titles = new List<Title>()
                            {
                                new Title() { LatinTitle = metadataSources ?? "", IsMainTitle = true }
                            }
                        });

                        song.Titles.Add(new Title() { LatinTitle = metadataTitle ?? "", IsMainTitle = true });

                        song.Artists.Add(new SongArtist()
                        {
                            Titles = new List<Title>(metadataArtists.Select(x =>
                                new Title() { LatinTitle = x, IsMainTitle = true })),
                        });

                        song.Links.Add(new SongLink()
                        {
                            Duration = TimeSpan.FromSeconds(60),
                            IsVideo = false,
                            Url = $"emqlocalmusiclibrary{filePath.Replace("G:/Music", "").Replace("G:\\Music", "")}"
                        });

                        song.StartTime = song.DetermineSongStartTime(Quiz.Room.QuizSettings.Filters);
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine($"TagLib exception for {filePath}: " + e.Message);
                    }
                }

                if (dbSongs.Count == 0)
                {
                    return false;
                }

                foreach (Song dbSong in dbSongs)
                {
                    dbSong.PlayerLabels = GetPlayerLabelsForSong(dbSong, vndbInfos);
                }

                Quiz.Songs = dbSongs;
                Quiz.QuizState.NumSongs = Quiz.Songs.Count;
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }

        foreach (Song dbSong in dbSongs)
        {
            dbSong.Links = SongLink.FilterSongLinks(dbSong.Links, Quiz.Room.QuizSettings.Filters.IsPreferLongLinks);
        }

        // Console.WriteLine(JsonSerializer.Serialize(Quiz.Songs));
        Quiz.QuizState.ExtraInfo = "Waiting buffering...";

        return true;
    }

    public async Task StartQuiz()
    {
        Quiz.QuizState.QuizStatus = QuizStatus.Playing;

        // await EnterQuiz();
        // HubContext.Clients.Clients(Quiz.Room.AllPlayerConnectionIds.Values).SendAsync("ReceiveQuizStarted");

        switch (Quiz.Room.QuizSettings.SongSelectionKind)
        {
            case SongSelectionKind.Random:
            case SongSelectionKind.SpacedRepetition:
            case SongSelectionKind.LocalMusicLibrary:
                await EnterQuiz();
                await EnterGuessingPhase();
                TypedQuizHub.ReceiveQuizStarted(Quiz.Room.Players.Concat(Quiz.Room.Spectators).Select(x => x.Id));
                break;
            case SongSelectionKind.Looting:
                await EnterLootingPhase();
                TypedQuizHub.ReceivePyramidEntered(Quiz.Room.Players.Concat(Quiz.Room.Spectators).Select(x => x.Id));
                await Task.Delay(TimeSpan.FromSeconds(1));
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }

        await SetTimer();
    }

    public async Task OnSendPlayerIsBuffered(int playerId, string source)
    {
        Player? player = Quiz.Room.Players.SingleOrDefault(player => player.Id == playerId);
        if (player == null)
        {
            // early return if spectator
            return;
        }

        player.IsBuffered = true;
        int isBufferedCount = Quiz.Room.Players.Count(x => x.IsBuffered);
        Quiz.Room.Log($"isBufferedCount: {isBufferedCount} Source: {source}", playerId, writeToConsole: false);
    }

    public async Task OnSendPlayerJoinedQuiz(string connectionId, int playerId)
    {
        if (Quiz.QuizState.QuizStatus == QuizStatus.Playing)
        {
            TypedQuizHub.ReceiveQuizStarted(new[] { playerId });

            // todo player initialization logic shouldn't be here at all after the user + player separation
            var player = Quiz.Room.Players.SingleOrDefault(x => x.Id == playerId);
            if (player == null)
            {
                // early return if spectator
                return;
            }

            if (player.Score > 0 || player.Guess != null || (Quiz.Room.QuizSettings.MaxLives > 0 &&
                                                             player.Lives != Quiz.Room.QuizSettings.MaxLives))
            {
                return;
            }

            player.Lives = Quiz.Room.QuizSettings.MaxLives;
            player.Score = 0;
            player.AnsweringKind = Quiz.Room.QuizSettings.AnsweringKind == AnsweringKind.Mixed
                ? player.AnsweringKind
                : Quiz.Room.QuizSettings.AnsweringKind;
            player.Guess = null;
            player.IsGuessKindCorrectDict = null;
            player.IsBuffered = false;
            player.IsSkipping = false;
            player.IsReadiedUp = player.IsBot;
            player.PlayerStatus = PlayerStatus.Default;
            player.NGMCGuessesCurrent = player.NGMCGuessesInitial;
            player.NGMCCanBurn = false;
            player.NGMCMustPick = false;
            player.NGMCCanBePicked = false;
            player.NGMCMustBurn = false;

            if (Quiz.Room.QuizSettings.TeamSize > 1)
            {
                var teammate = Quiz.Room.Players.FirstOrDefault(x => x.TeamId == player.TeamId);
                if (teammate != null)
                {
                    player.Lives = teammate.Lives;

                    // don't think having these checks make much sense because you can't hotjoin those gamemodes anyways
                    if (Quiz.Room.QuizSettings.GamemodeKind != GamemodeKind.NGMC &&
                        Quiz.Room.QuizSettings.GamemodeKind != GamemodeKind.EruMode)
                    {
                        player.Score = teammate.Score;
                    }
                }
            }

            TypedQuizHub.ReceiveUpdateRoom(Quiz.Room.Players.Concat(Quiz.Room.Spectators).Select(x => x.Id), Quiz.Room,
                false);
        }
    }

    public async Task OnSendGuessChanged(int playerId, string? guess, GuessKind guessKind)
    {
        // don't allow players to change guesses in shared-guesses teamed games after team guesses have been determined
        if (Quiz.QuizState.Phase is QuizPhaseKind.Guess || (Quiz.QuizState.Phase is QuizPhaseKind.Judgement &&
                                                            !Quiz.QuizState.TeamGuessesHaveBeenDetermined))
        {
            var player = Quiz.Room.Players.SingleOrDefault(x => x.Id == playerId);
            if (player != null)
            {
                if (Quiz.QuizState.Phase is QuizPhaseKind.Judgement)
                {
                    if (player.PlayerStatus is PlayerStatus.Correct or PlayerStatus.Wrong)
                    {
                        // for lagging players
                        return;
                    }
                }

                guess = guess == null ? "" : guess[..Math.Min(guess.Length, Constants.MaxGuessLength)];

                // MUST BE DONE AFTER THE SUBSTRING IN ORDER TO AVOID SLICING SURROGATE PAIRS IN HALF
                int firstInvalidUnicodeCharIndex = guess.FirstInvalidUnicodeSequenceIndex();
                if (firstInvalidUnicodeCharIndex >= 0)
                {
                    guess = guess.RemoveInvalidUnicodeSequences(firstInvalidUnicodeCharIndex);
                }

                player.Guess ??= new PlayerGuess();
                player.PlayerStatus = PlayerStatus.Guessed;
                player.Guess.Dict[guessKind] = guess;

                if (player.Guess.DictFirstGuessMs[guessKind] <= 0)
                {
                    switch (Quiz.QuizState.Phase)
                    {
                        case QuizPhaseKind.Guess:
                            player.Guess.DictFirstGuessMs[guessKind] =
                                Quiz.Room.QuizSettings.GuessMs - (int)Quiz.QuizState.RemainingMs;
                            break;
                        case QuizPhaseKind.Judgement:
                            player.Guess.DictFirstGuessMs[guessKind] = Quiz.Room.QuizSettings.GuessMs;
                            break;
                        case QuizPhaseKind.Results:
                        case QuizPhaseKind.Looting:
                        default:
                            throw new ArgumentOutOfRangeException();
                    }
                }

                if (Quiz.Room.QuizSettings.IsSharedGuessesTeams)
                {
                    // also includes the player themselves
                    var teammates = Quiz.Room.Players.Where(x => x.TeamId == player.TeamId).ToArray();

                    var teammateIds = teammates.Select(x => x.Id);
                    var teammateGuesses = Quiz.Room.PlayerGuesses.Where(x => teammateIds.Contains(x.Key));
                    var dict = teammateGuesses.ToDictionary(x => x.Key, x => x.Value);

                    TypedQuizHub.ReceivePlayerGuesses(teammateIds, dict);
                }
                else
                {
                    TypedQuizHub.ReceivePlayerGuesses(new[] { playerId },
                        Quiz.Room.PlayerGuesses.Where(x => x.Key == playerId).ToDictionary(x => x.Key, x => x.Value));
                }

                TypedQuizHub.ReceiveUpdateRoom(Quiz.Room.Players.Concat(Quiz.Room.Spectators).Select(x => x.Id),
                    Quiz.Room, false);
            }
            else
            {
                Quiz.Room.Log("invalid guess submitted", playerId);
            }
        }
    }

    public async Task OnSendTogglePause()
    {
        if (Quiz.QuizState.QuizStatus == QuizStatus.Playing &&
            !Quiz.QuizState.ExtraInfo.Contains("Waiting buffering") &&
            !Quiz.QuizState.ExtraInfo.Contains("Skipping")) // todo
        {
            if (Quiz.QuizState.IsPaused)
            {
                Quiz.QuizState.IsPaused = false;
                Quiz.Room.Log("Unpaused", -1, true);
            }
            else
            {
                Quiz.QuizState.IsPaused = true;
                Quiz.Room.Log("Paused", -1, true);
            }

            TypedQuizHub.ReceiveUpdateRoom(Quiz.Room.Players.Concat(Quiz.Room.Spectators).Select(x => x.Id), Quiz.Room,
                false);
        }
    }

    // public async Task OnSendPlayerLeaving(int playerId)
    // {
    //
    // }

    private async Task EnterLootingPhase()
    {
        var rng = Random.Shared;

        Quiz.QuizState.Phase = QuizPhaseKind.Looting;
        Quiz.QuizState.RemainingMs = Quiz.Room.QuizSettings.LootingMs;

        TreasureRoom[][] GenerateTreasureRooms(Dictionary<string, List<Title>> validSources)
        {
            int gridSize = Quiz.Room.Players.Count switch
            {
                <= 3 => 3,
                4 or 5 => 4,
                6 or 7 => 5,
                8 or 9 => 6,
                >= 10 => 7,
            };

            TreasureRoom[][] treasureRooms =
                new TreasureRoom[gridSize].Select(_ => new TreasureRoom[gridSize]).ToArray();
            for (int i = 0; i < gridSize; i++)
            {
                for (int j = 0; j < gridSize; j++)
                {
                    treasureRooms[i][j] =
                        new TreasureRoom() { Coords = new Point(i, j), Treasures = new List<Treasure>() };

                    if (j - 1 >= 0 && j - 1 < gridSize)
                    {
                        treasureRooms[i][j].Exits.Add(Direction.North, new Point(i, j - 1));
                    }

                    if (i + 1 < gridSize)
                    {
                        treasureRooms[i][j].Exits.Add(Direction.East, new Point(i + 1, j));
                    }

                    if (j + 1 < gridSize)
                    {
                        treasureRooms[i][j].Exits.Add(Direction.South, new Point(i, j + 1));
                    }

                    if (i - 1 >= 0 && i - 1 < gridSize)
                    {
                        treasureRooms[i][j].Exits.Add(Direction.West, new Point(i - 1, j));
                    }
                }
            }

            Quiz.QuizState.LootingGridSize = gridSize;

            foreach (var player in Quiz.Room.Players)
            {
                player.PlayerStatus = PlayerStatus.Looting;
                player.LootingInfo = new PlayerLootingInfo
                {
                    X = LootingConstants.TreasureRoomWidth / 2,
                    Y = LootingConstants.TreasureRoomHeight / 2,
                    Inventory = new List<Treasure>(),
                    TreasureRoomCoords =
                        new Point(rng.Next(Quiz.QuizState.LootingGridSize),
                            rng.Next(Quiz.QuizState.LootingGridSize)),
                };
            }

            const int treasureMaxX = LootingConstants.TreasureRoomWidth - LootingConstants.PlayerAvatarSize;
            const int treasureMaxY = LootingConstants.TreasureRoomHeight - LootingConstants.PlayerAvatarSize;
            foreach (var dbSong in validSources)
            {
                var treasure = new Treasure(
                    Guid.NewGuid(),
                    dbSong,
                    new Point(rng.Next(treasureMaxX), rng.Next(treasureMaxY)));

                // todo max treasures in one room?
                // todo better position randomization?
                var treasureRoomId = new Point(rng.Next(0, gridSize), rng.Next(0, gridSize));
                treasureRooms[treasureRoomId.X][treasureRoomId.Y].Treasures.Add(treasure);
            }

            // Console.WriteLine("treasureRooms: " + JsonSerializer.Serialize(treasureRooms)][ Utils.Jso);
            return treasureRooms;
        }

        Quiz.Room.TreasureRooms = GenerateTreasureRooms(Quiz.ValidSourcesForLooting);
        // HubContext.Clients.Clients(Quiz.Room.AllPlayerConnectionIds.Values).SendAsync("ReceivePyramidEntered");

        // todo
    }

    private async Task<bool> SetLootedSongs()
    {
        var validSources = new Dictionary<int, List<string>>();
        foreach (var player in Quiz.Room.Players)
        {
            validSources[player.Id] = new List<string>();
            foreach (var treasure in player.LootingInfo.Inventory)
            {
                validSources[player.Id].Add(treasure.ValidSource.Key);
            }
        }

        int distinctSourcesCount = validSources.SelectMany(x => x.Value).Distinct().Count();
        if (distinctSourcesCount == 0)
        {
            return false;
        }

        Quiz.Room.Log($"Players looted {distinctSourcesCount} distinct sources");

        var dbSongs = await DbManager.GetRandomSongs(
            Quiz.Room.QuizSettings.NumSongs,
            Quiz.Room.QuizSettings.Duplicates,
            validSources.SelectMany(x => x.Value).ToList(),
            Quiz.Room.QuizSettings.Filters,
            players: Quiz.Room.Players.ToList(),
            ownerUserId: Quiz.Room.Owner.Id,
            gamemodeKind: Quiz.Room.QuizSettings.GamemodeKind,
            songSelectionKind: Quiz.Room.QuizSettings.SongSelectionKind);

        if (!dbSongs.Any())
        {
            return false;
        }

        Quiz.Room.Log($"Selected {dbSongs.Count} looted songs");

        Quiz.Songs = dbSongs;
        Quiz.QuizState.NumSongs = Quiz.Songs.Count;

        foreach (Song dbSong in dbSongs)
        {
            dbSong.Links = SongLink.FilterSongLinks(dbSong.Links, Quiz.Room.QuizSettings.Filters.IsPreferLongLinks);

            // todo merge this with ValidSourcesForLooting and get rid of this
            var currentSongSourceVndbUrls = dbSong.Sources
                .SelectMany(x => x.Links.Where(y => y.Type == SongSourceLinkType.VNDB))
                .Select(z => z.Url)
                .ToList();

            var lootedPlayers = validSources.Where(x => x.Value.Any(y => currentSongSourceVndbUrls.Contains(y)))
                .ToDictionary(x => x.Key, x => x.Value);

            var playerLabels = new Dictionary<int, List<Label>>();
            foreach (KeyValuePair<int, List<string>> lootedPlayer in lootedPlayers)
            {
                var newLabel = new Label
                {
                    Id = -1,
                    IsPrivate = false,
                    Name = "Looted",
                    VNs = new Dictionary<string, int> { { currentSongSourceVndbUrls.First(), -1 } },
                    Kind = LabelKind.Include
                };

                playerLabels.Add(lootedPlayer.Key, new List<Label> { newLabel });
            }

            dbSong.PlayerLabels = playerLabels;
        }

        return true;
    }

    public async Task OnSendPlayerMoved(Player player, int newX, int newY, DateTime dateTime,
        string connectionId)
    {
        // todo anti-cheat
        player.LootingInfo.X = newX;
        player.LootingInfo.Y = newY;

        TypedQuizHub.ReceiveUpdatePlayerLootingInfo(
            Quiz.Room.Players.Where(x => x.Id != player.Id).Select(y => y.Id),
            player.Id, player.LootingInfo with { Inventory = new List<Treasure>() }, true);
    }

    public async Task OnSendPickupTreasure(Session session, Guid treasureGuid)
    {
        if (!Quiz.Room.TreasureRooms.Any())
        {
            return;
        }

        var player = session.Player;
        if (player.LootingInfo.TreasureRoomCoords.X < Quiz.QuizState.LootingGridSize &&
            player.LootingInfo.TreasureRoomCoords.Y < Quiz.QuizState.LootingGridSize)
        {
            var treasureRoom = Quiz.Room.TreasureRooms[player.LootingInfo.TreasureRoomCoords.X][
                player.LootingInfo.TreasureRoomCoords.Y];
            var treasure = treasureRoom.Treasures.SingleOrDefault(x => x.Guid == treasureGuid);

            if (treasure != null)
            {
                if (treasure.Position.IsReachableFromCoords(player.LootingInfo.X, player.LootingInfo.Y))
                {
                    if (player.LootingInfo.Inventory.Count < Quiz.Room.QuizSettings.InventorySize)
                    {
                        player.LootingInfo.Inventory.Add(treasure);
                        treasureRoom.Treasures.Remove(treasure);

                        TypedQuizHub.ReceiveUpdateTreasureRoom(
                            Quiz.Room.Players.Concat(Quiz.Room.Spectators).Select(x => x.Id), treasureRoom);

                        TypedQuizHub.ReceiveUpdateRemainingMs(new[] { session.Player.Id }, Quiz.QuizState.RemainingMs);

                        TypedQuizHub.ReceiveUpdatePlayerLootingInfo(new[] { session.Player.Id }, player.Id,
                            player.LootingInfo,
                            false);
                    }
                }
                else
                {
                    Quiz.Room.Log(
                        $"Player is not close enough to the treasure to pickup: {player.LootingInfo.X},{player.LootingInfo.Y} -> " +
                        $"{treasure.Position.X},{treasure.Position.Y}", player.Id);
                }
            }
            else
            {
                Quiz.Room.Log(
                    $"Could not find the treasure {treasureGuid} to pickup at {treasureRoom.Coords.X},{treasureRoom.Coords.Y}");
            }
        }
        else
        {
            Quiz.Room.Log("Invalid player treasure room coords", player.Id);
        }
    }

    public async Task OnSendDropTreasure(Session session, Guid treasureGuid)
    {
        var player = Quiz.Room.Players.Single(x => x.Id == session.Player.Id);
        var treasure = player.LootingInfo.Inventory.SingleOrDefault(x => x.Guid == treasureGuid);
        if (treasure is not null)
        {
            var treasureRoom = Quiz.Room.TreasureRooms[player.LootingInfo.TreasureRoomCoords.X][
                player.LootingInfo.TreasureRoomCoords.Y];

            int newX = Math.Clamp(
                player.LootingInfo.X +
                Random.Shared.Next(-LootingConstants.PlayerAvatarSize, LootingConstants.PlayerAvatarSize),
                0, LootingConstants.TreasureRoomWidth - LootingConstants.PlayerAvatarSize);

            int newY = Math.Clamp(
                player.LootingInfo.Y +
                Random.Shared.Next(-LootingConstants.PlayerAvatarSize, LootingConstants.PlayerAvatarSize),
                0, LootingConstants.TreasureRoomHeight - LootingConstants.PlayerAvatarSize);

            player.LootingInfo.Inventory.Remove(treasure);
            treasureRoom.Treasures.Add(treasure with { Position = new Point(newX, newY) });

            TypedQuizHub.ReceiveUpdateTreasureRoom(Quiz.Room.Players.Concat(Quiz.Room.Spectators).Select(x => x.Id),
                treasureRoom);

            TypedQuizHub.ReceiveUpdateRemainingMs(new[] { session.Player.Id }, Quiz.QuizState.RemainingMs);

            TypedQuizHub.ReceiveUpdatePlayerLootingInfo(new[] { session.Player.Id }, player.Id, player.LootingInfo,
                false);
        }
    }

    public async Task OnSendChangeTreasureRoom(Session session, Point treasureRoomCoords, Direction direction)
    {
        if (!Quiz.Room.TreasureRooms.Any())
        {
            // looting phase probably has ended already
            return;
        }

        var player = Quiz.Room.Players.SingleOrDefault(x => x.Id == session.Player.Id) ??
                     Quiz.Room.Spectators.Single(x => x.Id == session.Player.Id);

        bool alreadyInTheRoom =
            player.LootingInfo.X == treasureRoomCoords.X && player.LootingInfo.Y == treasureRoomCoords.Y;
        if (alreadyInTheRoom)
        {
            TypedQuizHub.ReceiveUpdatePlayerLootingInfo(new[] { session.Player.Id }, player.Id, player.LootingInfo,
                true);
            return;
        }

        var currentTreasureRoom =
            Quiz.Room.TreasureRooms[player.LootingInfo.TreasureRoomCoords.X][
                player.LootingInfo.TreasureRoomCoords.Y];
        var newTreasureRoom =
            Quiz.Room.TreasureRooms[treasureRoomCoords.X][treasureRoomCoords.Y];

        if (treasureRoomCoords.X < Quiz.QuizState.LootingGridSize &&
            treasureRoomCoords.Y < Quiz.QuizState.LootingGridSize)
        {
            if (currentTreasureRoom.Exits.ContainsValue(treasureRoomCoords))
            {
                player.LootingInfo.TreasureRoomCoords = treasureRoomCoords;

                int newX = player.LootingInfo.X;
                int newY = player.LootingInfo.Y;
                switch (direction)
                {
                    case Direction.North:
                    case Direction.South:
                        newY = Math.Clamp(LootingConstants.TreasureRoomHeight - player.LootingInfo.Y, 0,
                            LootingConstants.TreasureRoomHeight - LootingConstants.PlayerAvatarSize);
                        break;
                    case Direction.East:
                    case Direction.West:
                        newX = Math.Clamp(LootingConstants.TreasureRoomWidth - player.LootingInfo.X, 0,
                            LootingConstants.TreasureRoomWidth - LootingConstants.PlayerAvatarSize);
                        break;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(direction), direction, null);
                }

                player.LootingInfo.X = Math.Clamp(newX, 0,
                    LootingConstants.TreasureRoomWidth - LootingConstants.PlayerAvatarSize);
                player.LootingInfo.Y = Math.Clamp(newY, 0,
                    LootingConstants.TreasureRoomHeight - LootingConstants.PlayerAvatarSize);

                TypedQuizHub.ReceiveUpdateTreasureRoom(new[] { session.Player.Id }, newTreasureRoom);

                TypedQuizHub.ReceiveUpdateRemainingMs(new[] { session.Player.Id }, Quiz.QuizState.RemainingMs);

                TypedQuizHub.ReceiveUpdatePlayerLootingInfo(new[] { session.Player.Id }, player.Id, player.LootingInfo,
                    true);

                TypedQuizHub.ReceiveUpdatePlayerLootingInfo(
                    Quiz.Room.Players.Where(x => x.Id != session.Player.Id).Select(y => y.Id),
                    player.Id, player.LootingInfo with { Inventory = new List<Treasure>() }, true);
            }
            else
            {
                Quiz.Room.Log(
                    $"Failed to use non-existing exit {player.LootingInfo.TreasureRoomCoords.X},{player.LootingInfo.TreasureRoomCoords.Y} -> " +
                    $"{treasureRoomCoords.X},{treasureRoomCoords.Y}", player.Id);
                // Console.WriteLine(JsonSerializer.Serialize(Quiz.Room.TreasureRooms[player.LootingInfo.TreasureRoomCoords.X][player.LootingInfo.TreasureRoomCoords.Y].Exits));
            }
        }
        else
        {
            Quiz.Room.Log($"Failed to move to non-existing treasure room {treasureRoomCoords}", player.Id);
        }
    }

    public async Task OnSendToggleSkip(string connectionId, int playerId)
    {
        if (!Quiz.QuizState.IsPaused && (Quiz.Room.QuizSettings.GamemodeKind == GamemodeKind.Radio ||
                                         (Quiz.QuizState.QuizStatus == QuizStatus.Playing &&
                                          Quiz.QuizState.RemainingMs > 2000 &&
                                          Quiz.QuizState.Phase is QuizPhaseKind.Guess or QuizPhaseKind.Results)))
        {
            var player = Quiz.Room.Players.Single(x => x.Id == playerId);
            player.IsSkipping = !player.IsSkipping;

            TypedQuizHub.ReceiveUpdateRoom(Quiz.Room.Players.Concat(Quiz.Room.Spectators).Select(x => x.Id), Quiz.Room,
                false);
            await TriggerSkipIfNecessary();
        }
    }

    private async Task TriggerSkipIfNecessary()
    {
        if (Quiz.QuizState.RemainingMs <= 500)
        {
            return;
        }

        var activeSessions = ServerState.Sessions.Where(x => Quiz.Room.Players.Any(y => y.Id == x.Player.Id))
            .Where(x => x.Player.HasActiveConnectionQuiz).ToList();
        int isSkippingCount = activeSessions.Count(x => x.Player.IsSkipping);

        const float skipConst = 0.8f;
        int skipNumber = (int)Math.Round(activeSessions.Count * skipConst, MidpointRounding.AwayFromZero);

        Quiz.Room.Log($"isSkippingCount: {isSkippingCount}/{skipNumber}", writeToConsole: false);
        if (isSkippingCount >= skipNumber)
        {
            if (Quiz.QuizState.Phase is QuizPhaseKind.Guess)
            {
                if (activeSessions.Any(x => !x.Player.IsSkipping))
                {
                    Quiz.Room.Log("not skipping because not everyone wants to skip", writeToConsole: false);
                    return;
                }
            }

            Quiz.QuizState.RemainingMs = 500;
            Quiz.QuizState.ExtraInfo = "Skipping...";
            Quiz.Room.Log($"Skipping...");

            foreach (Player p in Quiz.Room.Players)
            {
                p.IsSkipping = false;
            }

            TypedQuizHub.ReceiveUpdateRoom(Quiz.Room.Players.Concat(Quiz.Room.Spectators).Select(x => x.Id), Quiz.Room,
                false);
        }
    }

    public async Task OnConnectedAsync(int playerId, string connectionId)
    {
        if (Quiz.QuizState.QuizStatus is QuizStatus.Playing)
        {
            await OnSendPlayerIsBuffered(playerId, "OnConnectedAsync");
        }
    }

    private static Dictionary<int, List<Label>> GetPlayerLabelsForSong(Song song,
        Dictionary<int, PlayerVndbInfo> vndbInfos)
    {
        // todo? this could be written in a more efficient (batched) manner
        Dictionary<int, List<Label>> playerLabels = new();
        foreach ((int playerId, PlayerVndbInfo? vndbInfo) in vndbInfos)
        {
            if (vndbInfo.Labels != null)
            {
                var excludedVns = vndbInfo.Labels.Where(x => x is { Kind: LabelKind.Exclude })
                    .SelectMany(x => x.VNs.Select(y => y.Key));
                playerLabels[playerId] = new List<Label>();
                foreach (Label label in vndbInfo.Labels.Where(x => x.Kind == LabelKind.Include))
                {
                    var currentSongSourceVndbUrls = song.Sources
                        .SelectMany(x => x.Links.Where(y => y.Type == SongSourceLinkType.VNDB))
                        .Select(z => z.Url)
                        .ToList();

                    if (currentSongSourceVndbUrls.Any(x => label.VNs.ContainsKey(x) && !excludedVns.Contains(x)))
                    {
                        // todo? add preference for showing private labels as is
                        if (label.IsPrivate)
                        {
                            var newLabel = new Label
                            {
                                Id = -1,
                                IsPrivate = true,
                                Name = "Private Label",
                                VNs = label.VNs.Where(x => currentSongSourceVndbUrls.Contains(x.Key))
                                    .ToDictionary(x => x.Key, x => x.Value),
                                Kind = label.Kind
                            };
                            playerLabels[playerId].Add(newLabel);
                        }
                        else
                        {
                            var newLabel = new Label
                            {
                                Id = label.Id,
                                IsPrivate = label.IsPrivate,
                                Name = label.Name,
                                VNs = label.VNs.Where(x => currentSongSourceVndbUrls.Contains(x.Key))
                                    .ToDictionary(x => x.Key, x => x.Value),
                                Kind = label.Kind
                            };
                            playerLabels[playerId].Add(newLabel);
                        }
                    }
                }
            }
        }

        return playerLabels;
    }

    public static async Task<(UserSpacedRepetition previous, UserSpacedRepetition current)> DoSpacedRepetition(
        int userId, int musicId, bool isCorrect)
    {
        var previous = await DbManager.GetPreviousSpacedRepetitionInfo(userId, musicId) ??
                       new UserSpacedRepetition();

        // Very rough implementation of an earliness nerf,
        // which is necessary in EMQ's case as most songs will be reviewed very early most of the time.
        // We don't care about lateness.
        float previousDays = previous.interval_days; // backup old state for UI
        float earlyDays = (float)(previous.due_at - DateTime.UtcNow).TotalDays;
        if (earlyDays > 1)
        {
            previous.interval_days /= 1.5f;
        }

        var current = previous.DoSM2(isCorrect);
        current.user_id = userId;
        current.music_id = musicId;
        current.reviewed_at = DateTime.UtcNow;
        current.due_at = DateTime.UtcNow.AddHours(current.interval_days * 24);

        previous.interval_days = previousDays; // restore old state for UI
        bool success = await DbManager.UpsertEntity(current);
        return (previous, current);
    }
}
