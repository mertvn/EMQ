using System.Collections.Generic;
using System.Drawing;

namespace EMQ.Shared.Quiz.Entities.Concrete;

public class TreasureRoom
{
    public Point Coords { get; set; }

    public List<Treasure> Treasures { get; set; } = new();

    public Dictionary<Direction, Point> Exits { get; set; } = new();
}

public enum Direction
{
    North,
    East,
    South,
    West,
    // Northeast,
    // Northwest,
    // Southeast,
    // Southwest
}
