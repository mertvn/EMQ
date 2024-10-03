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

    public int?[] Composer { get; init; }

    public int?[] Arranger { get; init; }

    public int?[] Lyricist { get; init; }

    public override string ToString() => MusicId.ToString();
}

public readonly struct EgsDataCreater
{
    public string Id { get; init; }

    public string Name { get; init; }

    public string Furigana { get; init; }
}

public readonly struct XRef
{
    public string Vndb { get; init; }

    public string Vgmdb { get; init; }

    public string Anison { get; init; }

    public string Egs { get; init; }

    public override string ToString()
    {
        return $"{Vndb},{Vgmdb},{Anison},{Egs}";
    }
}

public readonly struct Credit
{
    public int MusicId { get; init; }

    public string VndbId { get; init; }

    public SongArtistRole Type { get; init; }

    public override string ToString()
    {
        return $"{MusicId},{VndbId},{Type.ToString()}";
    }
}
