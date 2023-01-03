using System;
using System.Drawing;
using System.Numerics;

namespace EMQ.Shared.Quiz.Entities.Concrete;

public record Treasure
{
    public Guid Guid { get; set; }

    public int TreasureRoomId { get; set; }

    public Song Song { get; set; }

    public Point Position { get; set; }

    public bool IsGrabbableFromCoords(int x, int y)
    {
        const int grabbingRange = 78;
        if (Math.Abs(Position.X - x) < grabbingRange &&
            Math.Abs(Position.Y - y) < grabbingRange)
        {
            return true;
        }

        return false;
    }
}
