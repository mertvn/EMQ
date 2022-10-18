namespace BlazorApp1.Shared.Quiz.Dto.Response;

public class ResNextSong
{
    public ResNextSong(int songIndex, string url)
    {
        SongIndex = songIndex;
        Url = url;
    }

    public int SongIndex { get; }

    public string Url { get; }
}
