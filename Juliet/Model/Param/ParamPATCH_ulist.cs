namespace Juliet.Model.Param;

public class ParamPATCH_ulist : ParamPATCHDELETE
{
    public int[] labels_set { get; set; } = Array.Empty<int>();

    public int[] labels_unset { get; set; } = Array.Empty<int>();
}
