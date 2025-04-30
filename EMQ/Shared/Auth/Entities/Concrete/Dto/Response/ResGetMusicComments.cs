using System;
using System.Collections.Generic;
using EMQ.Shared.Core.SharedDbEntities;

namespace EMQ.Shared.Auth.Entities.Concrete.Dto.Response;

public class ResGetMusicComments
{
    public Dictionary<int, string> UsernamesDict { get; set; } = new();

    public MusicComment[] MusicComments { get; set; } = Array.Empty<MusicComment>();
}
