using VNDBStaffNotesParser;

namespace EMQ.Server.Db.Imports.VNDB;

public class ProcessedMusic
{
    public string VNID { get; set; } = null!;

    public string title { get; set; } = null!;

    public string StaffID { get; set; } = null!;

    public int ArtistAliasID { get; set; }

    public string name { get; set; } = null!;

    public string role { get; set; } = null!;

    public ParsedSong ParsedSong { get; set; } = null!;

    public string[] ProducerIds { get; set; } = null!;
}
