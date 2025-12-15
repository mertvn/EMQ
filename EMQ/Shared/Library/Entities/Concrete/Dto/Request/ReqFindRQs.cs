using System;
using EMQ.Shared.Quiz.Entities.Concrete;

namespace EMQ.Shared.Library.Entities.Concrete.Dto.Request;

public class ReqFindRQs
{
    public ReqFindRQs(DateTime startDate, DateTime endDate, SongSourceSongType[] ssst, bool isShowAutomatedEdits,
        ReviewQueueStatus[] status)
    {
        StartDate = startDate;
        EndDate = endDate;
        SSST = ssst;
        IsShowAutomatedEdits = isShowAutomatedEdits;
        Status = status;
    }

    public DateTime StartDate { get; }

    public DateTime EndDate { get; }

    public SongSourceSongType[] SSST { get; }

    public bool IsShowAutomatedEdits { get; }

    public ReviewQueueStatus[] Status { get; }
}
