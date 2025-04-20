using System;
using System.Collections.Generic;
using EMQ.Shared.Core.SharedDbEntities;

namespace EMQ.Shared.Auth.Entities.Concrete.Dto.Response;

public class ResGetMusicVotes
{
    public Dictionary<int, string> UsernamesDict { get; set; } = new();

    public MusicVote[] MusicVotes { get; set; } = Array.Empty<MusicVote>();
}
