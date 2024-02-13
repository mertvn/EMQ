using System;

namespace EMQ.Shared.Quiz.Entities.Concrete.Dto.Response;

public class ResSyncRoomWithTime
{
    public Room? Room { get; set; }

    public DateTime? Time { get; set; }
}
