using EMQ.Shared.Quiz.Entities.Concrete;

namespace EMQ.Shared.Mod.Entities.Concrete.Dto.Request;

public class ReqUpdateReviewQueueItem
{
    public ReqUpdateReviewQueueItem(int rqId, ReviewQueueStatus reviewQueueStatus, string? notes)
    {
        RQId = rqId;
        ReviewQueueStatus = reviewQueueStatus;
        Notes = notes ?? "";
    }

    public int RQId { get; set; }

    public ReviewQueueStatus ReviewQueueStatus { get; set; }

    public string Notes { get; set; }
}
