using VNDBStaffNotesParser;

namespace EMQ.Server.Db.Imports;

public class ProcessedMusic
{
    public string VNID { get; set; }

    public string title { get; set; }

    public string StaffID { get; set; }

    public int ArtistAliasID { get; set; }

    public string name { get; set; }

    public string role { get; set; }

    public ParsedSong ParsedSong { get; set; }
}
