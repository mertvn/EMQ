namespace EMQ.Shared.Quiz.Entities.Concrete;

public class CategoryFilter
{
    public CategoryFilter(SongSourceCategory songSourceCategory, LabelKind trilean)
    {
        SongSourceCategory = songSourceCategory;
        Trilean = trilean;
    }

    public static QuizFilterKind QuizFilterKind => QuizFilterKind.Category;

    public SongSourceCategory SongSourceCategory { get; }

    public LabelKind Trilean { get; } // todo actual trilean type
}
