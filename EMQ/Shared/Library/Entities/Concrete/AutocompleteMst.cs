using System.Text.Json.Serialization;

namespace EMQ.Shared.Library.Entities.Concrete;

public class AutocompleteMst
{
    public AutocompleteMst(int msId, string mstLatinTitle, string mstNonLatinTitle = "",
        string mstLatinTitleNormalized = "", string mstNonLatinTitleNormalized = "")
    {
        MSId = msId;
        MSTLatinTitle = mstLatinTitle;
        MSTNonLatinTitle = mstNonLatinTitle;
        MSTLatinTitleNormalized = mstLatinTitleNormalized;
        MSTNonLatinTitleNormalized = mstNonLatinTitleNormalized;
    }

    [JsonPropertyName("msId")]
    public int MSId { get; set; }

    [JsonPropertyName("mstLT")]
    public string MSTLatinTitle { get; set; }

    [JsonPropertyName("mstNLT")]
    public string MSTNonLatinTitle { get; set; }

    [JsonPropertyName("mstLTNorm")]
    public string MSTLatinTitleNormalized { get; set; }

    [JsonPropertyName("mstNLTNorm")]
    public string MSTNonLatinTitleNormalized { get; set; }
}
