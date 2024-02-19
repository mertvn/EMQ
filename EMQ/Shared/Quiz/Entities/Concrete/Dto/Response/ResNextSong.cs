namespace EMQ.Shared.Quiz.Entities.Concrete.Dto.Response;

public class ResNextSong
{
    public ResNextSong(int songIndex, string url, int startTime, string screenshotUrl, string coverUrl)
    {
        SongIndex = songIndex;
        Url = url;
        StartTime = startTime;
        ScreenshotUrl = screenshotUrl;
        CoverUrl = coverUrl;
    }

    public int SongIndex { get; }

    public string Url { get; }

    public int StartTime { get; }

    public string ScreenshotUrl { get; }

    public string CoverUrl { get; }
}
