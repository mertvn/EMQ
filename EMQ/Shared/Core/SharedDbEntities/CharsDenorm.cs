using System.ComponentModel.DataAnnotations.Schema;
using EMQ.Shared.Quiz.Entities.Concrete;

namespace EMQ.Shared.Core.SharedDbEntities;

[Table("chars_denorm")]
public class CharsDenorm
{
    public string vid { get; set; } = "";

    public string cid { get; set; } = "";

    public string image { get; set; } = "";

    public string? latin { get; set; }

    public string name { get; set; } = "";

    public VndbCharRoleKind? role { get; set; }
}
