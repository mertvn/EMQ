using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using Juliet.Model.Filters;
using Juliet.Model.VNDBObject.Fields;

namespace Juliet.Model.Param;

public class ParamPOST_ulist : Param
{
    public IEnumerable<FieldPOST_ulist> Fields { get; set; } = new List<FieldPOST_ulist>();

    [DefaultValue(true)]
    public bool NormalizedFilters { get; set; } = true;

    [DefaultValue(false)]
    public bool CompactFilters { get; set; } = false;

    /// <summary>
    /// This parameter will be ignored if Exhaust is set to true.
    /// </summary>
    [Range(0, Constants.MaxResultsPerPage)]
    [DefaultValue(Constants.MaxResultsPerPage)]
    public int ResultsPerPage { get; set; } = Constants.MaxResultsPerPage;

    /// <summary>
    /// Grab all results?
    /// </summary>
    [DefaultValue(true)]
    public bool Exhaust { get; set; } = true;

    public Combinator? Filters { get; set; }
}
