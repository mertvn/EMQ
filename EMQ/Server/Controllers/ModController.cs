using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Dapper;
using Dapper.Database.Extensions;
using EMQ.Server.Db;
using EMQ.Server.Db.Entities;
using EMQ.Server.Db.Entities.Auth;
using EMQ.Server.Db.Imports.MusicBrainz;
using EMQ.Server.Db.Imports.VNDB;
using EMQ.Shared.Auth.Entities.Concrete;
using EMQ.Shared.Auth.Entities.Concrete.Dto.Response;
using EMQ.Shared.Core;
using EMQ.Shared.Mod.Entities.Concrete.Dto.Request;
using EMQ.Shared.Quiz.Entities.Concrete;
using Microsoft.AspNetCore.Mvc;
using Npgsql;

namespace EMQ.Server.Controllers;

[ApiController]
[Route("[controller]")]
public class ModController : ControllerBase
{
    [CustomAuthorize(PermissionKind.Moderator)]
    [HttpGet]
    [Route("ExportSongLite")]
    public async Task<ActionResult<string>> ExportSongLite()
    {
        string songLite = await DbManager.ExportSongLite();
        return songLite;
    }

    [CustomAuthorize(PermissionKind.Moderator)]
    [HttpGet]
    [Route("ExportSongLite_MB")]
    public async Task<ActionResult<string>> ExportSongLite_MB()
    {
        string songLite = await DbManager.ExportSongLite_MB();
        return songLite;
    }

    [CustomAuthorize(PermissionKind.Admin)]
    [HttpPost]
    [Route("RunGc")]
    public async Task<ActionResult> RunGc()
    {
        ServerUtils.RunAggressiveGc();
        return Ok();
    }

    [CustomAuthorize(PermissionKind.Admin)]
    [HttpPost]
    [Route("RunAnalysis")]
    public async Task<ActionResult> RunAnalysis()
    {
        if (ServerState.Config.IsServerReadOnly)
        {
            return Unauthorized();
        }

        await ServerUtils.RunAnalysis();
        return Ok();
    }

    [CustomAuthorize(PermissionKind.Admin)]
    [HttpPost]
    [Route("StartCountdown")]
    public async Task<ActionResult> StartCountdown(ReqStartCountdown req)
    {
        if (ServerState.Config.IsServerReadOnly)
        {
            return Unauthorized();
        }

        ServerState.CountdownInfo = new CountdownInfo { Message = req.Message, DateTime = req.DateTime };
        return Ok();
    }

    [CustomAuthorize(PermissionKind.ReviewSongLink)]
    [HttpPost]
    [Route("UpdateReviewQueueItem")]
    public async Task<ActionResult> UpdateReviewQueueItem([FromBody] ReqUpdateReviewQueueItem req)
    {
        if (ServerState.Config.IsServerReadOnly)
        {
            return Unauthorized();
        }

        // todo use return value
        bool success = await DbManager.UpdateReviewQueueItem(req.RQId, req.ReviewQueueStatus, reason: req.Notes,
            analyserResult: null);
        return Ok();
    }

    [CustomAuthorize(PermissionKind.ReviewEdit)]
    [HttpPost]
    [Route("UpdateEditQueueItem")]
    public async Task<ActionResult> UpdateEditQueueItem([FromBody] ReqUpdateReviewQueueItem req)
    {
        if (ServerState.Config.IsServerReadOnly)
        {
            return Unauthorized();
        }

        await using var connection = new NpgsqlConnection(ConnectionHelper.GetConnectionString());
        await connection.OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();
        bool success =
            await DbManager.UpdateEditQueueItem(transaction, req.RQId, req.ReviewQueueStatus, reason: req.Notes);
        if (success)
        {
            await transaction.CommitAsync();
            return Ok();
        }

        return StatusCode(409);
    }

    [CustomAuthorize(PermissionKind.Admin)]
    [HttpPost]
    [Route("SetServerConfig")]
    public async Task<ActionResult> ToggleIsSubmissionDisabled(ServerConfig req)
    {
        var session = AuthStuff.GetSession(HttpContext.Items);
        if (session is null)
        {
            return Unauthorized();
        }

        Console.WriteLine(
            $"{session.Player.Username} SetServerConfig {JsonSerializer.Serialize(ServerState.Config)} -> {JsonSerializer.Serialize(req)}");
        ServerState.Config = req;
        if (!req.AllowGuests)
        {
            foreach (var sess in ServerState.Sessions.Where(x => x.Player.Id >= Constants.PlayerIdGuestMin))
            {
                await ServerState.RemoveSession(sess, "SetServerConfig");
            }
        }

        return Ok();
    }

    [CustomAuthorize(PermissionKind.Admin)]
    [HttpPost]
    [Route("SetSubmittedBy")]
    public async Task<ActionResult> SetSubmittedBy([FromBody] ReqSetSubmittedBy req)
    {
        if (ServerState.Config.IsServerReadOnly)
        {
            return Unauthorized();
        }

        foreach (string url in req.Urls)
        {
            int rows = await DbManager.SetSubmittedBy(url.UnReplaceSelfhostLink(), req.SubmittedBy);
            if (rows > 0)
            {
                Console.WriteLine($"set {url} submitted_by to {req.SubmittedBy}");
            }
            else
            {
                Console.WriteLine($"failed setting {url} submitted_by to {req.SubmittedBy}");
                return StatusCode(500);
            }
        }

        return Ok();
    }

    [CustomAuthorize(PermissionKind.DeleteSongLink)]
    [HttpPost]
    [Route("DeleteSongLink")]
    public async Task<ActionResult<int>> DeleteSongLink([FromBody] ReqDeleteSongLink req)
    {
        if (ServerState.Config.IsServerReadOnly)
        {
            return Unauthorized();
        }

        var session = AuthStuff.GetSession(HttpContext.Items);
        if (session is null)
        {
            return Unauthorized();
        }

        int rows = await DbManager.DeleteMusicExternalLink(req.MId, req.Url);
        rows += await DbManager.DeleteMusicExternalLink(req.MId, req.Url.UnReplaceSelfhostLink());
        if (rows > 0)
        {
            Console.WriteLine($"{session.Player.Username} DeleteSongLink {req.MId} {req.Url}");
        }

        return rows;
    }

    [CustomAuthorize(PermissionKind.Delete)]
    [HttpPost]
    [Route("DeleteArtist")]
    public async Task<ActionResult> DeleteArtist([FromBody] int aId)
    {
        if (ServerState.Config.IsServerReadOnly)
        {
            return Unauthorized();
        }

        var session = AuthStuff.GetSession(HttpContext.Items);
        if (session is null)
        {
            return Unauthorized();
        }

        var artist = await DbManager.GetEntity<Artist>(aId);
        bool success = await DbManager.DeleteEntity(artist!);
        if (success)
        {
            Console.WriteLine($"{session.Player.Username} DeleteArtist {aId}");
            // todo evict all songs with this artist
            await DbManager.EvictFromSongsCache(aId);
        }

        return success ? Ok() : StatusCode(500);
    }

    [CustomAuthorize(PermissionKind.EditUser)]
    [HttpPost]
    [Route("EditUser")]
    public async Task<ActionResult> EditUser(ResGetPublicUserInfo req)
    {
        if (ServerState.Config.IsServerReadOnly)
        {
            return Unauthorized();
        }

        var session = AuthStuff.GetSession(HttpContext.Items);
        if (session is null)
        {
            return Unauthorized();
        }

        await using var connectionAuth = new NpgsqlConnection(ConnectionHelper.GetConnectionString_Auth());
        await connectionAuth.OpenAsync();
        await using var transactionAuth = await connectionAuth.BeginTransactionAsync();
        int rows = await connectionAuth.ExecuteAsync(
            "UPDATE users set (roles, ign_mv, inc_perm, exc_perm) = (@roles, @ign_mv, @inc, @exc) where id = @uid",
            new
            {
                roles = (int)req.UserRoleKind,
                ign_mv = req.IgnMv,
                inc = req.IncludedPermissions.Select(x => (int)x).ToArray(),
                exc = req.ExcludedPermissions.Select(x => (int)x).ToArray(),
                uid = req.UserId
            }, transactionAuth);

        bool success = rows == 1;
        if (!success)
        {
            return StatusCode(500);
        }

        Console.WriteLine($"{session.Player.Username} EditUser {JsonSerializer.Serialize(req, Utils.Jso)}");
        await transactionAuth.CommitAsync();
        var userSession = ServerState.Sessions.FirstOrDefault(x => x.Player.Id == req.UserId);
        if (userSession != null)
        {
            var dbUser = await connectionAuth.GetAsync<User>(req.UserId);
            userSession.UserRoleKind = dbUser.roles;
            userSession.IncludedPermissions = dbUser.inc_perm?.ToList();
            userSession.ExcludedPermissions = dbUser.exc_perm?.ToList();
        }

        return Ok();
    }

    [CustomAuthorize(PermissionKind.ReviewEdit)]
    [HttpPost]
    [Route("DeleteArtistAlias")]
    public async Task<ActionResult> DeleteArtistAlias(SongArtist req)
    {
        if (ServerState.Config.IsServerReadOnly)
        {
            return Unauthorized();
        }

        var session = AuthStuff.GetSession(HttpContext.Items);
        if (session is null)
        {
            return Unauthorized();
        }

        bool success = await DbManager.DeleteArtistAlias(req.Id, req.Titles.Single().ArtistAliasId);
        Console.WriteLine($"{session.Player.Username} DeleteArtistAlias {JsonSerializer.Serialize(req, Utils.Jso)}");
        return success ? Ok() : StatusCode(500);
    }

    [CustomAuthorize(PermissionKind.Admin)]
    [HttpPost]
    [Route("ForceRemoveRoom")]
    public async Task<ActionResult> ForceRemoveRoom([FromBody] Guid roomId)
    {
        var session = AuthStuff.GetSession(HttpContext.Items);
        if (session is null)
        {
            return Unauthorized();
        }

        var room = ServerState.Rooms.SingleOrDefault(x => x.Id == roomId);
        if (room == null)
        {
            return StatusCode(500);
        }

        ServerState.RemoveRoom(room, "ForceRemoveRoom");
        Console.WriteLine(
            $"{session.Player.Username} ForceRemoveRoom r{roomId} {room.Name} {string.Join(", ", room.Players.Select(x => x.Username))}");
        return Ok();
    }
}
