using System;

namespace EMQ.Shared.Library.Entities.Concrete.Dto.Request;

public class ReqFindRQs
{
    public ReqFindRQs(DateTime startDate, DateTime endDate)
    {
        StartDate = startDate;
        EndDate = endDate;
    }

    public DateTime StartDate { get; }

    public DateTime EndDate { get; }
}
