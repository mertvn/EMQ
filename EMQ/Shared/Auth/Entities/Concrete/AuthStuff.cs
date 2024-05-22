using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;

namespace EMQ.Shared.Auth.Entities.Concrete;

public static class AuthStuff // todo? find better name. maybe AuthConstants?
{
    public const int MinPasswordLength = 20;

    public const int MaxPasswordLength = 64;

    public const int RegisterTokenValidMinutes = 60;

    public const int ResetPasswordTokenValidMinutes = 60;

    public static readonly TimeSpan MaxSessionAge = TimeSpan.FromDays(1);

    public static readonly string AuthorizationHeaderName = HttpRequestHeader.Authorization.ToString();

    public static PermissionKind[] DefaultVisitorPermissions { get; } =
    {
        PermissionKind.Visitor, PermissionKind.Login, PermissionKind.SearchLibrary, PermissionKind.ViewStats
    };

    public static PermissionKind[] DefaultGuestPermissions { get; } =
        DefaultVisitorPermissions.Concat(new[]
        {
            PermissionKind.Guest, PermissionKind.CreateRoom, PermissionKind.PlayQuiz,
            PermissionKind.SendChatMessage, PermissionKind.UpdatePreferences
        }).ToArray();

    public static PermissionKind[] DefaultUserPermissions { get; } =
        DefaultGuestPermissions.Concat(new[]
        {
            PermissionKind.User, PermissionKind.JoinRanked, PermissionKind.UploadSongLink,
            PermissionKind.ReportSongLink, PermissionKind.StoreQuizSettings
        }).ToArray();

    public static PermissionKind[] DefaultImportHelperPermissions { get; } =
        DefaultUserPermissions.Concat(new[] { PermissionKind.ImportHelper }).ToArray();

    public static PermissionKind[] DefaultModeratorPermissions { get; } =
        DefaultUserPermissions.Concat(new[] { PermissionKind.Moderator }).ToArray();

    public static PermissionKind[] DefaultChatModeratorPermissions { get; } =
        DefaultModeratorPermissions.Concat(new[] { PermissionKind.ModerateChat }).ToArray();

    public static PermissionKind[] DefaultReviewQueueModeratorPermissions { get; } =
        DefaultModeratorPermissions.Concat(new[] { PermissionKind.ReviewSongLink }).ToArray();

    public static PermissionKind[] DefaultDatabaseModeratorPermissions { get; } =
        DefaultModeratorPermissions.Concat(new[] { PermissionKind.EditSongMetadata }).ToArray();

    public static PermissionKind[] DefaultAdminPermissions { get; } = Enum.GetValues<PermissionKind>();

    public static Dictionary<UserRoleKind, PermissionKind[]> DefaultRolePermissionsDict { get; } =
        new()
        {
            { UserRoleKind.Visitor, DefaultVisitorPermissions },
            { UserRoleKind.Guest, DefaultGuestPermissions },
            { UserRoleKind.User, DefaultUserPermissions },
            { UserRoleKind.ImportHelper, DefaultImportHelperPermissions },
            { UserRoleKind.ChatModerator, DefaultChatModeratorPermissions },
            { UserRoleKind.ReviewQueueModerator, DefaultReviewQueueModeratorPermissions },
            { UserRoleKind.DatabaseModerator, DefaultDatabaseModeratorPermissions },
            { UserRoleKind.Admin, DefaultAdminPermissions },
        };

    // probably should cache the result of this or something,
    // unless we want to somehow make this user-based instead of role-based in the future
    public static bool HasPermission(UserRoleKind role, PermissionKind permission)
    {
        var allRoles = Enum.GetValues<UserRoleKind>();

        // List<UserRoleKind> userRoles =
        //     allRoles.Where(userRoleKind => session.UserRoleKind.HasFlag(userRoleKind)).ToList();

        List<UserRoleKind> userRoles = new();
        foreach (UserRoleKind userRoleKind in allRoles)
        {
            if (role.HasFlag(userRoleKind))
            {
                userRoles.Add(userRoleKind);
            }
        }

        // var userPermissions = Session.DefaultRolePermissionsDict
        //     .Where(x => userRoles.Contains(x.Key))
        //     .SelectMany(y => y.Value).ToList();

        List<PermissionKind> userPermissions = new();
        foreach ((UserRoleKind key, PermissionKind[]? value) in AuthStuff.DefaultRolePermissionsDict)
        {
            if (userRoles.Contains(key))
            {
                userPermissions.AddRange(value);
            }
        }

        // Console.WriteLine($"userPermissions: {JsonSerializer.Serialize(userPermissions, Utils.JsoIndented)}");
        return userPermissions.Contains(permission);
    }

    // todo use everywhere relevant
    public static Session? GetSession(IDictionary<object, object?> httpContextItems)
    {
        httpContextItems.TryGetValue("EMQ_SESSION", out object? session);
        return session as Session;
    }
}

[Flags]
public enum UserRoleKind
{
    Visitor = 0, // not logged in
    Guest = 1 << 0, // logged in as a temporary guest
    User = 1 << 1, // logged in as a registered user
    ChatModerator = 1 << 2,
    ReviewQueueModerator = 1 << 3,
    DatabaseModerator = 1 << 4,
    ImportHelper = 1 << 5,
    Admin = int.MaxValue,
}

public enum PermissionKind
{
    None = 0, // do not use

    Visitor = 1000,
    Login = 1001,
    SearchLibrary = 1002,
    ViewStats = 1003,

    Guest = 2000,
    CreateRoom = 2001,
    PlayQuiz = 2002,
    SendChatMessage = 2003,
    UpdatePreferences = 2004,

    User = 3000,
    JoinRanked = 3001,
    UploadSongLink = 3002,
    ReportSongLink = 3003,
    StoreQuizSettings = 3004,

    ImportHelper = 3700,

    Moderator = 4000,

    ModerateChat = 5001,

    ReviewSongLink = 6001,

    EditSongMetadata = 7001,

    EditUsers = 8001,

    Admin = 9000,
}
