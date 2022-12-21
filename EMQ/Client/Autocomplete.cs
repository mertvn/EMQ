using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace EMQ.Client;

public static class Autocomplete
{
    public static IEnumerable<string> SearchAutocomplete(string arg, string[] data)
    {
        // todo startsWith should be for every word rather than just the first word
        // todo prefer Japanese latin titles
        var exactMatch = data.Where(x => string.Equals(x, arg, StringComparison.OrdinalIgnoreCase)).OrderBy(x => x);
        var startsWith = data.Where(x => x.ToLowerInvariant().StartsWith(arg.ToLowerInvariant())).OrderBy(x => x);
        var contains = data.Where(x => x.ToLowerInvariant().Contains(arg.ToLowerInvariant())).OrderBy(x => x);

        // _logger.LogInformation(JsonSerializer.Serialize(result));
        return exactMatch.Concat(startsWith.Concat(contains)).Distinct();
    }
}
