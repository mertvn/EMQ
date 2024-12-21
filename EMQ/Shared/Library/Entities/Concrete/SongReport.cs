using System;
using System.Collections.Generic;
using System.Linq;
using EMQ.Shared.Quiz.Entities.Concrete;

namespace EMQ.Shared.Library.Entities.Concrete;

public class SongReport
{
    public int id { get; set; }

    public int music_id { get; set; }

    public string url { get; set; } = "";

    public SongReportKind report_kind { get; set; }

    public string submitted_by { get; set; } = "";

    public DateTime submitted_on { get; set; }

    public ReviewQueueStatus status { get; set; }

    public string note_mod { get; set; } = "";

    public string note_user { get; set; } = "";

    public Song? Song { get; set; } = new();
}
