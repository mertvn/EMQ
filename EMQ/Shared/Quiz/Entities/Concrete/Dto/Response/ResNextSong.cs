namespace EMQ.Shared.Quiz.Entities.Concrete.Dto.Response;

public class ResNextSong
{
    public ResNextSong(int songIndex, string url, int startTime)
    {
        SongIndex = songIndex;
        Url = url;
        StartTime = startTime;
    }

    public int SongIndex { get; }

    public string Url { get; }

    public int StartTime { get; }
}
