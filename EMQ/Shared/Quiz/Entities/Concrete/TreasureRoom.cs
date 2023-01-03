using System;
using System.Collections.Generic;
using System.Numerics;

namespace EMQ.Shared.Quiz.Entities.Concrete;

public class TreasureRoom
{
    public int Id { get; set; }

    public List<Treasure> Treasures { get; set; } = new();
}
