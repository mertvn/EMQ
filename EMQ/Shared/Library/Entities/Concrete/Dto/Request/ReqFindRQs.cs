using System;
using EMQ.Shared.Quiz.Entities.Concrete;

namespace EMQ.Shared.Library.Entities.Concrete.Dto.Request;

public class ReqFindRQs
{
    public ReqFindRQs(DateTime startDate, DateTime endDate, SongSourceSongTypeMode ssstm)
    {
        StartDate = startDate;
        EndDate = endDate;
        SSSTM = ssstm;
    }

    public DateTime StartDate { get; }

    public DateTime EndDate { get; }

    public SongSourceSongTypeMode SSSTM { get; }
}
