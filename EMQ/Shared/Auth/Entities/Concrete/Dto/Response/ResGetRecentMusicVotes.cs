using System.Collections.Generic;

namespace EMQ.Shared.Auth.Entities.Concrete.Dto.Response;

public class ResGetRecentMusicVotes
{
    public Dictionary<int, string> SongsDict { get; set; } = new();

    public ResGetMusicVotes ResGetMusicVotes { get; set; } = new();
}
