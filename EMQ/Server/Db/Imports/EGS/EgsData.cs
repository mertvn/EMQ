using System.Collections.Generic;
using EMQ.Shared.Quiz.Entities.Concrete;

namespace EMQ.Server.Db.Imports.EGS;

public readonly struct EgsData
{
    public int MusicId { get; init; }

    public string MusicName { get; init; }

    public string MusicFurigana { get; init; }

    public string MusicPlaytime { get; init; }

    public SongSourceSongType GameMusicCategory { get; init; }

    public string GameName { get; init; }

    public string GameVndbUrl { get; init; }

    public string SingerCharacterName { get; init; }

    public bool SingerFeaturing { get; init; }

    public int CreaterId { get; init; }

    public List<string> CreaterNames { get; init; }

    public string CreaterFurigana { get; init; }
}
