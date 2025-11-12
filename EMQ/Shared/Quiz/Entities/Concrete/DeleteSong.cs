using EMQ.Shared.Quiz.Entities.Abstract;

namespace EMQ.Shared.Quiz.Entities.Concrete;

public class DeleteSong : IEditQueueEntity
{
    public int Id { get; set; }

    public string StrLatin { get; set; } = "";

    public string? NoteUser { get; set; } = "";

    public override string ToString() => $"DELETE em{Id} {StrLatin}";
}
