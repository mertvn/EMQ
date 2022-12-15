namespace EMQ.Shared.Library.Entities.Concrete.Dto.Request;

public class ReqFindSongsByArtistTitle
{
    public ReqFindSongsByArtistTitle(string artistTitle)
    {
        ArtistTitle = artistTitle;
    }

    public string ArtistTitle { get; }
}
