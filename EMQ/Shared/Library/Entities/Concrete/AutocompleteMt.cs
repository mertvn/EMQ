using System.Text.Json.Serialization;

namespace EMQ.Shared.Library.Entities.Concrete;

public class AutocompleteMt
{
    public AutocompleteMt(int mId, string mtLatinTitle, string mtLatinTitleNormalized = "", bool? isBGM = null,
        string mtNonLatinTitle = "", string mtNonLatinTitleNormalized = "")
    {
        MId = mId;
        MTLatinTitle = mtLatinTitle;
        MTLatinTitleNormalized = mtLatinTitleNormalized;
        IsBGM = isBGM;
        MTNonLatinTitle = mtNonLatinTitle;
        MTNonLatinTitleNormalized = mtNonLatinTitleNormalized;
    }

    [JsonPropertyName("1")]
    public int MId { get; set; }

    [JsonPropertyName("2")]
    public string MTLatinTitle { get; set; }

    [JsonPropertyName("3")]
    public string MTLatinTitleNormalized { get; set; }

    [JsonPropertyName("4")]
    public bool? IsBGM { get; set; }

    [JsonPropertyName("5")]
    public string MTNonLatinTitle { get; set; }

    [JsonPropertyName("6")]
    public string MTNonLatinTitleNormalized { get; set; }
}
