using System;
using System.Collections.Generic;
using EMQ.Shared.Library.Entities.Concrete;
using EMQ.Shared.Quiz.Entities.Concrete;

namespace EMQ.Shared.Erodle.Entities.Concrete;

public class ErodleAnswer
{
    public int ErodleId { get; set; }

    public int GuessNumber { get; set; }

    public AutocompleteMst AutocompleteMst { get; set; } = new(-1, "");

    public DateTime Date { get; set; }

    public List<SongSourceCategory> Tags { get; set; } = new();

    public List<SongSourceDeveloper> Developers { get; set; } = new();

    public int? Rating { get; set; }

    public int? VoteCount { get; set; }
}

public enum ErodleKind
{
    None,
    Mst,
}

public enum ErodleStatus
{
    Playing,
    Lost,
    Won,
}
