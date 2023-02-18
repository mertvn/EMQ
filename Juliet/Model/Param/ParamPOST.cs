using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using Juliet.Model.Filters;

namespace Juliet.Model.Param;

public class ParamPOST<T> : Param
{
    public IEnumerable<T> Fields { get; set; } = new List<T>();

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
