namespace EMQ.Shared.Quiz.Entities.Concrete.Dto.Request;

public class ReqNextSong
{
    public ReqNextSong(string playerToken, int songIndex, bool wantsVideo, SongLinkType host,
        CdnEdgeKind cdnEdge)
    {
        PlayerToken = playerToken;
        SongIndex = songIndex;
        WantsVideo = wantsVideo;
        Host = host;
        CdnEdge = cdnEdge;
    }

    public string PlayerToken { get; }

    public int SongIndex { get; }

    public bool WantsVideo { get; }

    public SongLinkType Host { get; }

    public CdnEdgeKind CdnEdge { get; }
}
