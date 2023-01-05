using System;
using System.Collections.Generic;
using System.Drawing;

namespace EMQ.Shared.Quiz.Entities.Concrete;

public class Treasure
{

    public Guid Guid { get; set; }

    public KeyValuePair<string, List<Title>> ValidSource { get; set; }

    public Point Position { get; set; }
}
