using System.Text.Json.Serialization;

namespace Juliet.Model.Param;

public class ParamPATCHDELETE
{
    /// <summary>
    /// VNDB API token <br/>
    /// Used in: PATCH_ulist, PATCH_rlist, DELETE_ulist, DELETE_rlist <br/>
    /// </summary>
    [JsonIgnore]
    public string? APIToken { get; set; }

    [JsonIgnore]
    public string Id { get; set; } = "";
}
