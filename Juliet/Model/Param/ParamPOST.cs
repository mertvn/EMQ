﻿using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using Juliet.Model.Filters;

namespace Juliet.Model.Param;

public class ParamPOST<T> : Param
{
    public IEnumerable<T> Fields { get; set; } = new List<T>();

    [DefaultValue(false)]
    public bool NormalizedFilters { get; set; } = false;

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
    // todo refactor to maxresults
    public bool Exhaust { get; set; } = true;

    public Query? Filters { get; set; }

    /// <summary>
    /// VNDB advsearch filter string, takes precedence over Filters if set. (Example: "023gjaN3830X1o")
    /// </summary>
    public string? RawFilters { get; set; }
}
