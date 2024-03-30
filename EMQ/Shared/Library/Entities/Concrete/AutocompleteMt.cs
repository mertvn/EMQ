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

    [JsonPropertyName("mId")]
    public int MId { get; set; }

    [JsonPropertyName("mtLT")]
    public string MTLatinTitle { get; set; }

    [JsonPropertyName("mtLTNorm")]
    public string MTLatinTitleNormalized { get; set; }
}
