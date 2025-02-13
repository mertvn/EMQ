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

    [JsonPropertyName("1")]
    public int MSId { get; set; }

    [JsonPropertyName("2")]
    public string MSTLatinTitle { get; set; }

    [JsonPropertyName("3")]
    public string MSTNonLatinTitle { get; set; }

    [JsonPropertyName("4")]
    public string MSTLatinTitleNormalized { get; set; }

    [JsonPropertyName("5")]
    public string MSTNonLatinTitleNormalized { get; set; }
}
