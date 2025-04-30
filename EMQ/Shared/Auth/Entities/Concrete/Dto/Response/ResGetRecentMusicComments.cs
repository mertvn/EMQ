using System.Collections.Generic;

namespace EMQ.Shared.Auth.Entities.Concrete.Dto.Response;

public class ResGetRecentMusicComments
{
    public Dictionary<int, string> SongsDict { get; set; } = new();

    public ResGetMusicComments ResGetMusicComments { get; set; } = new();
}
