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

    public static HashSet<PermissionKind> DefaultVisitorPermissions { get; } = new()
    {
        PermissionKind.Visitor, PermissionKind.Login, PermissionKind.SearchLibrary, PermissionKind.ViewStats
    };

    public static HashSet<PermissionKind> DefaultGuestPermissions { get; } =
        DefaultVisitorPermissions.Concat(new HashSet<PermissionKind>
        {
            PermissionKind.Guest,
            PermissionKind.CreateRoom,
            PermissionKind.PlayQuiz,
            PermissionKind.SendChatMessage,
            PermissionKind.UpdatePreferences
        }).ToHashSet();

    public static HashSet<PermissionKind> DefaultUserPermissions { get; } =
        DefaultGuestPermissions.Concat(new HashSet<PermissionKind>
        {
            PermissionKind.User,
            PermissionKind.JoinRanked,
            PermissionKind.UploadSongLink,
            PermissionKind.ReportSongLink,
            PermissionKind.StoreQuizSettings,
            PermissionKind.Vote,
            PermissionKind.Edit
        }).ToHashSet();

    public static HashSet<PermissionKind> DefaultImportHelperPermissions { get; } =
        DefaultUserPermissions.Concat(new HashSet<PermissionKind> { PermissionKind.ImportHelper }).ToHashSet();

    public static HashSet<PermissionKind> DefaultModeratorPermissions { get; } =
        DefaultUserPermissions.Concat(new HashSet<PermissionKind> { PermissionKind.Moderator }).ToHashSet();

    public static HashSet<PermissionKind> DefaultChatModeratorPermissions { get; } =
        DefaultModeratorPermissions.Concat(new HashSet<PermissionKind> { PermissionKind.ModerateChat }).ToHashSet();

    public static HashSet<PermissionKind> DefaultReviewQueueModeratorPermissions { get; } =
        DefaultModeratorPermissions
            .Concat(new HashSet<PermissionKind> { PermissionKind.ReviewSongLink, PermissionKind.DeleteSongLink })
            .ToHashSet();

    public static HashSet<PermissionKind> DefaultDatabaseModeratorPermissions { get; } =
        DefaultModeratorPermissions
            .Concat(new HashSet<PermissionKind> { PermissionKind.DeleteSongLink, PermissionKind.Delete })
            .ToHashSet();

    public static HashSet<PermissionKind> DefaultAdminPermissions { get; } =
        Enum.GetValues<PermissionKind>().ToHashSet();

    // MUST contain all possible UserRoleKind values
    public static Dictionary<UserRoleKind, HashSet<PermissionKind>> DefaultRolePermissionsDict { get; } =
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

    public static bool HasPermission(Session? session, PermissionKind permission)
    {
        var sessionRole = session?.UserRoleKind ?? UserRoleKind.Visitor;

        HashSet<PermissionKind> userPermissions = new();
        foreach ((UserRoleKind role, HashSet<PermissionKind> permissions) in DefaultRolePermissionsDict)
        {
            if ((sessionRole & role) == role)
            {
                userPermissions.UnionWith(permissions);
            }
        }

        if (session != null)
        {
            if (session.IncludedPermissions != null)
            {
                userPermissions.UnionWith(session.IncludedPermissions);
            }

            if (session.ExcludedPermissions != null)
            {
                userPermissions.ExceptWith(session.ExcludedPermissions);
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
    Vote = 3005,
    Edit = 3006,

    ImportHelper = 3700,

    Moderator = 4000,

    ModerateChat = 5001,

    ReviewSongLink = 6001,
    DeleteSongLink = 6002,

    Delete = 7001,

    EditUsers = 8001,

    Admin = 9000,
}
