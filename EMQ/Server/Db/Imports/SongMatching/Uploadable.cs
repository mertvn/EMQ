using EMQ.Shared.Quiz.Entities.Concrete;

namespace EMQ.Server.Db.Imports.SongMatching;

public struct Uploadable
{
    public string Path { get; set; }

    public int MId { get; init; }

    public string? ResultUrl { get; set; }

    public SongLite SongLite { get; set; }
}
