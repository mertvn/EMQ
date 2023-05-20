using System.Text.Json.Serialization;

namespace EMQ.Shared.Library.Entities.Concrete;

public class AutocompleteA
{
    public AutocompleteA(int aId, string vndbId, string aaLatinAlias, string aaNonLatinAlias = "")
    {
        AId = aId;
        VndbId = vndbId;
        AALatinAlias = aaLatinAlias;
        AANonLatinAlias = aaNonLatinAlias;
    }

    [JsonPropertyName("aId")]
    public int AId { get; set; }

    [JsonPropertyName("vndbId")]
    public string VndbId { get; set; }

    [JsonPropertyName("aaLA")]
    public string AALatinAlias { get; set; }

    [JsonPropertyName("aaNLA")]
    public string AANonLatinAlias { get; set; }
}
