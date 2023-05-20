using EMQ.Shared.Library.Entities.Concrete;

namespace EMQ.Shared.Quiz.Entities.Concrete;

public class ArtistFilter
{
    public ArtistFilter(AutocompleteA artist, LabelKind trilean)
    {
        Artist = artist;
        Trilean = trilean;
    }

    public AutocompleteA Artist { get; }

    public LabelKind Trilean { get; set; } // todo actual trilean type
}
