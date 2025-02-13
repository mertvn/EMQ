using System.Text.Json.Serialization;

namespace EMQ.Shared.Library.Entities.Concrete;

public class AutocompleteMt
{
    public AutocompleteMt(int mId, string mtLatinTitle, string mtLatinTitleNormalized = "")
    {
        MId = mId;
        MTLatinTitle = mtLatinTitle;
        MTLatinTitleNormalized = mtLatinTitleNormalized;
    }

    [JsonPropertyName("1")]
    public int MId { get; set; }

    [JsonPropertyName("2")]
    public string MTLatinTitle { get; set; }

    [JsonPropertyName("3")]
    public string MTLatinTitleNormalized { get; set; }
}
