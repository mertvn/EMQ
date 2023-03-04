using System;

namespace EMQ.Shared.Quiz.Entities.Concrete;

public class RQ
{
    public int id { get; set; }

    public int music_id { get; set; }

    public string url { get; set; } = "";

    public SongLinkType type { get; set; }

    public bool is_video { get; set; }

    public string submitted_by { get; set; } = "";

    public DateTime submitted_on { get; set; }

    public ReviewQueueStatus status { get; set; }

    public string? reason { get; set; }

    public string? analysis { get; set; }

    public Song Song { get; set; } = new();

    public TimeSpan? duration { get; set; }
}
