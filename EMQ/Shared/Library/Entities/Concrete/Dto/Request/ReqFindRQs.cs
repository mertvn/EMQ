using System;
using EMQ.Shared.Quiz.Entities.Concrete;

namespace EMQ.Shared.Library.Entities.Concrete.Dto.Request;

public class ReqFindRQs
{
    public ReqFindRQs(DateTime startDate, DateTime endDate, SongSourceSongTypeMode ssstm, bool isShowAutomatedEdits,
        ReviewQueueStatus[] status)
    {
        StartDate = startDate;
        EndDate = endDate;
        SSSTM = ssstm;
        IsShowAutomatedEdits = isShowAutomatedEdits;
        Status = status;
    }

    public DateTime StartDate { get; }

    public DateTime EndDate { get; }

    public SongSourceSongTypeMode SSSTM { get; }

    public bool IsShowAutomatedEdits { get; }

    public ReviewQueueStatus[] Status { get; }
}
