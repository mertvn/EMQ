using System.Collections.Generic;

namespace EMQ.Shared.Quiz.Entities.Concrete;

public class SongMini
{
    public int Id { get; init; }

    public string S { get; init; } = "";

    public List<SongLink> L { get; init; } = new();

    public string A { get; init; } = "";
}
