using System.Text.Json.Serialization;

namespace EMQ.Shared.Library.Entities.Concrete;

public struct AutocompleteA
{
    public AutocompleteA(int aId, string aaLatinAlias, string aaNonLatinAlias = "")
    {
        AId = aId;
        AALatinAlias = aaLatinAlias;
        AANonLatinAlias = aaNonLatinAlias;
    }

    [JsonPropertyName("aId")]
    public int AId { get; set; }

    [JsonPropertyName("aaLA")]
    public string AALatinAlias { get; set; }

    [JsonPropertyName("aaNLA")]
    public string AANonLatinAlias { get; set; }
}
