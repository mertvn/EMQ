using System;

namespace EMQ.Shared.Quiz.Entities.Concrete;

public class BufferingInfo
{
    public int PlayerId { get; set; }

    public Guid RoomId { get; set; }

    public int nextSp { get; set; }

    public string Url { get; set; } = "";

    // this only shows whether the task was completed without being cancelled,
    // it doesn't show whether the player was actually able to buffer correctly
    public bool Success { get; set; }

    public DateTime StartjsTime { get; set; }

    public DateTime EndjsTime { get; set; }

    public TimeSpan TotaljsTime => EndjsTime - StartjsTime;

    public string CancellationReason { get; set; } = "";

    public string? Data { get; set; }
}
