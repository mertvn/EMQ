using EMQ.Shared.Quiz.Entities.Concrete;

namespace EMQ.Shared.Library.Entities.Concrete.Dto.Request;

public class ReqImportSongLink
{
    public ReqImportSongLink(int mId, SongLink songLink)
    {
        MId = mId;
        SongLink = songLink;
    }

    public int MId { get; }

    public SongLink SongLink { get; }
}
