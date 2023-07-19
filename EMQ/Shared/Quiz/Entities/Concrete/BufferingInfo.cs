using System;

namespace EMQ.Shared.Quiz.Entities.Concrete;

public class BufferingInfo
{
    public string PlayerToken { get; set; } = "";

    public Guid RoomId { get; set; }

    public int sp { get; set; }

    public bool Success { get; set; }

    public DateTime StartjsTime { get; set; }

    public DateTime EndjsTime { get; set; }

    public TimeSpan TotaljsTime => EndjsTime - StartjsTime;

    public string CancellationReason { get; set; } = "";

    public string Data { get; set; }
}
