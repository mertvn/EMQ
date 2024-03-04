using System;

namespace EMQ.Shared.Quiz.Entities.Concrete.Dto.Request;

public class ReqGetSHRoomContainers
{
    public ReqGetSHRoomContainers(int userId, DateTime startDate, DateTime endDate)
    {
        UserId = userId;
        StartDate = startDate;
        EndDate = endDate;
    }

    public int UserId { get; }

    public DateTime StartDate { get; }

    public DateTime EndDate { get; }
}
