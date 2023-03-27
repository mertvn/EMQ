using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace EMQ.Client.Pages;

// inside joke please ignore
public partial class Hasucana
{
    private static Dictionary<string, string> Dict { get; set; } = new()
    {
        { "a", "の" },
        { "e", "ま" },
        { "g", "ム" },
        { "h", "ん" },
        { "l", "し" },
        { "m", "ホ" },
        { "o", "ロ" },
        { "p", "ヤ" },
        { "r", "イ" },
        { "s", "ら" },
        { "t", "ナ" },
        { "u", "ひ" },
        { "v", "レ" },
        { "y", "と" },
    };

    private string InputForEncode { get; set; } = "sa";

    private string InputForDecode { get; set; } = "のら";

    public static string Encode(string s)
    {
        var sb = new StringBuilder(s);
        for (int index = 0; index < sb.Length; index++)
        {
            char c = sb[index];
            if (Dict.ContainsKey(c.ToString().ToLowerInvariant()))
            {
                sb[index] = Dict[c.ToString().ToLowerInvariant()][0];
            }
        }

        return sb.ToString();
    }

    public static string Decode(string s)
    {
        var sb = new StringBuilder(s);
        for (int index = 0; index < sb.Length; index++)
        {
            char c = sb[index];
            if (Dict.ContainsValue(c.ToString()))
            {
                sb[index] = Dict.Single(x => x.Value == c.ToString()).Key[0];
            }
        }

        return sb.ToString();
    }
}
