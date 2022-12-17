using EMQ.Shared.Quiz.Entities.Concrete;

namespace EMQ.Shared.Library.Entities.Concrete.Dto.Request;

public class ReqImportSongLink
{
    public ReqImportSongLink(int mId, SongLink songLink, string submittedBy)
    {
        MId = mId;
        SongLink = songLink;
        SubmittedBy = submittedBy;
    }

    public int MId { get; }

    public SongLink SongLink { get; }

    public string SubmittedBy { get; }
}
