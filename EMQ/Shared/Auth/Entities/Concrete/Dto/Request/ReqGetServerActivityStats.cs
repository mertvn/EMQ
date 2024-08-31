using System;

namespace EMQ.Shared.Auth.Entities.Concrete.Dto.Request;

public class ReqGetServerActivityStats
{
    public ReqGetServerActivityStats(DateTime startDate, DateTime endDate)
    {
        StartDate = startDate;
        EndDate = endDate;
    }

    public DateTime StartDate { get; }

    public DateTime EndDate { get; }
}
