using EMQ.Shared.Quiz.Entities.Concrete;

namespace EMQ.Shared.Core.UI;

public static class UiHelpers
{
    public static string Bool2Vis(bool b) => b ? "visible" : "hidden";

    public static string Bool2Display(bool b) => b ? "initial" : "none";

    public static string Bool2Color(bool? b, string trueValue, string falseValue, string nullValue = "")
    {
        if (b is null)
        {
            return nullValue;
        }
        else
        {
            return (bool)b ? trueValue : falseValue;
        }
    }

    public static string Bools2Color(bool b1, bool b2, string b1TrueValue, string b2TrueValue, string neitherValue = "")
    {
        if (b1)
        {
            return b1TrueValue;
        }
        else if (b2)
        {
            return b2TrueValue;
        }
        else
        {
            return neitherValue;
        }
    }

    public static string Bool2PointerEvents(bool b) => b ? "auto" : "none";

    public static string Bool2CursorPointer(bool b) => b ? "pointer" : "auto";

    public static string Bool2CursorNotAllowed(bool b) => b ? "not-allowed" : "auto";

    public static string Bool2Text(bool? b, string trueValue, string falseValue, string nullValue = "")
    {
        if (b is null)
        {
            return nullValue;
        }
        else
        {
            return (bool)b ? trueValue : falseValue;
        }
    }

    public static string ArtistRole2Color(SongArtistRole role)
    {
        return role switch
        {
            SongArtistRole.Unknown => "gray",
            SongArtistRole.Vocals => "lightgreen",
            SongArtistRole.Composer => "red",
            SongArtistRole.Arranger => "turquoise",
            SongArtistRole.Lyricist => "orange",
            _ => "inherit"
        };
    }

    public static string ArtistRole2Str(SongArtistRole role)
    {
        return role switch
        {
            SongArtistRole.Unknown => "U",
            SongArtistRole.Vocals => "V",
            SongArtistRole.Composer => "C",
            SongArtistRole.Arranger => "A",
            SongArtistRole.Lyricist => "L",
            _ => "?"
        };
    }
}
