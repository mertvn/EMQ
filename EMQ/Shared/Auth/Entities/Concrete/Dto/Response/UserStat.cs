using System;

namespace EMQ.Shared.Auth.Entities.Concrete.Dto.Response;

public class UserStat
{
    public int Id { get; set; }

    public string Username { get; set; } = "";

    public DateTime CreatedAt { get; set; }

    public int Played { get; set; }

    public int Votes { get; set; }
}
