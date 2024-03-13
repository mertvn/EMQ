using System;
using System.ComponentModel.DataAnnotations;
using EMQ.Shared.Core;

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

    [Range(typeof(DateTime), Constants.SHDateMin, Constants.QFDateMax,
        ErrorMessage = $"Start date must be in range of {Constants.SHDateMin} to {Constants.QFDateMax}")]
    public DateTime StartDate { get; }

    [Range(typeof(DateTime), Constants.SHDateMin, Constants.QFDateMax,
        ErrorMessage = $"Start date must be in range of {Constants.SHDateMin} to {Constants.QFDateMax}")]
    public DateTime EndDate { get; }
}
