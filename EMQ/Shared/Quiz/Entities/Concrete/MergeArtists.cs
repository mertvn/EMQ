using EMQ.Shared.Quiz.Entities.Abstract;

namespace EMQ.Shared.Quiz.Entities.Concrete;

public class MergeArtists : IEditQueueEntity
{
    public int Id { get; set; }

    public int SourceId { get; set; }

    public string SourceName { get; set; } = "";

    public override string ToString() => $"MERGE {SourceName}";
}
