using System.ComponentModel.DataAnnotations.Schema;

namespace EMQ.Shared.Core.SharedDbEntities;

[Table("staff_denorm")]
public class StaffDenorm
{
    public string vid { get; set; } = "";

    public string sid { get; set; } = "";

    public string alias_id { get; set; } = "";

    public string detail_id { get; set; } = "";

    public string? latin { get; set; }

    public string name { get; set; } = "";

    public StaffDenormRoleKind? role { get; set; }

    public string? role_detail { get; set; }
}

public enum StaffDenormRoleKind
{
    Unknown,
    Seiyuu,
}
