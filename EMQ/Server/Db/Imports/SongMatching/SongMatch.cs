using System.Collections.Generic;

namespace EMQ.Server.Db.Imports.SongMatching;

public readonly struct SongMatch
{
    public string Path { get; init; }

    public List<string> Sources { get; init; }

    public List<string> Titles { get; init; }

    public List<string> Artists { get; init; }
}

public class SongMatchInnerResult
{
    public SongMatch SongMatch { get; set; }

    public List<int> aIds { get; set; } = new();

    public List<int> mIds { get; set; } = new();

    public SongMatchInnerResultKind ResultKind { get; set; }
}

public enum SongMatchInnerResultKind
{
    NoSources,
    NoAids,
    MultipleMids,
    NoMids,
    Matched,
    AlreadyHave,
    Duplicate,
}
