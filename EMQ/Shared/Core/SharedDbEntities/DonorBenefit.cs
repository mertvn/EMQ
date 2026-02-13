using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Diagnostics.CodeAnalysis;

namespace EMQ.Shared.Core.SharedDbEntities;

[Table("donor_benefit")]
public class DonorBenefit
{
    [Key]
    public int user_id { get; set; }

    public bool show_donor_badge { get; set; }

    public string username_color { get; set; } = "";

    public UsernameAnimationKind username_animation { get; set; }
}

[SuppressMessage("ReSharper", "InconsistentNaming")]
public enum UsernameAnimationKind
{
    [Description("None")]
    none,

    [Description("Wave")]
    wave,

    [Description("Shake")]
    shake,

    [Description("Rainbow")]
    rainbow,

    [Description("Glow")]
    glow,

    [Description("Flash")]
    flash,

    [Description("Scroll")]
    scroll,

    [Description("Pulse")]
    pulse
}
