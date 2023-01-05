using System;
using System.Collections.Generic;
using System.Drawing;

namespace EMQ.Shared.Quiz.Entities.Concrete;

public record Treasure
{
    public Treasure(Guid guid, KeyValuePair<string, List<Title>> validSource, Point position)
    {
        Guid = guid;
        ValidSource = validSource;
        Position = position;
    }

    public Guid Guid { get; }

    public KeyValuePair<string, List<Title>> ValidSource { get; }

    public Point Position { get; init; }
}
