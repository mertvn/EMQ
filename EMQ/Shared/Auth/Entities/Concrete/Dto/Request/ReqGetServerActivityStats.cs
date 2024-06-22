using System;

namespace EMQ.Shared.Auth.Entities.Concrete.Dto.Request;

public class ReqGetServerActivityStats
{
    public ReqGetServerActivityStats(DateTime startDate, DateTime endDate, bool includeGuests)
    {
        StartDate = startDate;
        EndDate = endDate;
        IncludeGuests = includeGuests;
    }

    public DateTime StartDate { get; }

    public DateTime EndDate { get; }

    public bool IncludeGuests { get; }
}
