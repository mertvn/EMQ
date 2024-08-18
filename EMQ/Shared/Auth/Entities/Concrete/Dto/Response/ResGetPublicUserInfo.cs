using System;
using System.Collections.Generic;
using EMQ.Shared.Quiz.Entities.Concrete;

namespace EMQ.Shared.Auth.Entities.Concrete.Dto.Response;

public class ResGetPublicUserInfo
{
    public int UserId { get; set; }

    public int SongCount { get; set; }

    public float GuessRate { get; set; }

    public string Username { get; set; } = "";

    public Avatar Avatar { get; set; } = Avatar.DefaultAvatar;

    public UserRoleKind UserRoleKind { get; set; }

    public DateTime CreatedAt { get; set; }

    public Dictionary<SongSourceSongType, GetPublicUserInfoSSST> SSST { get; set; } = new();
}

public class GetPublicUserInfoSSST
{
    public SongSourceSongType Type { get; set; }

    public int Total { get; set; }

    public int Correct { get; set; }

    public float Percentage { get; set; }
}
