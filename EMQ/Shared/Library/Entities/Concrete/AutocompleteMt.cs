using System.Text.Json.Serialization;

namespace EMQ.Shared.Library.Entities.Concrete;

public class AutocompleteMt
{
    public AutocompleteMt(int mId, string mtLatinTitle, string mtLatinTitleNormalized = "", bool? isBGM = null)
    {
        MId = mId;
        MTLatinTitle = mtLatinTitle;
        MTLatinTitleNormalized = mtLatinTitleNormalized;
        IsBGM = isBGM;
    }

    [JsonPropertyName("1")]
    public int MId { get; set; }

    [JsonPropertyName("2")]
    public string MTLatinTitle { get; set; }

    [JsonPropertyName("3")]
    public string MTLatinTitleNormalized { get; set; }

    [JsonPropertyName("4")]
    public bool? IsBGM { get; set; }
}
