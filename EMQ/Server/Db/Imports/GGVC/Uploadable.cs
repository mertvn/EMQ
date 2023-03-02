using EMQ.Shared.Quiz.Entities.Concrete;

namespace EMQ.Server.Db.Imports.GGVC;

public struct Uploadable
{
    public string Path { get; init; }

    public int MId { get; init; }

    public string? ResultUrl { get; set; }

    public SongLite SongLite { get; set; }
}
