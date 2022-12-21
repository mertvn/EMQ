using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace EMQ.Client;

public static class Autocomplete
{
    public static IEnumerable<string> SearchAutocomplete(string arg, string[] autocompleteData)
    {
        // todo startsWith should be for every word rather than just the first word
        // todo prefer Japanese latin titles
        var exactMatch = autocompleteData.Where(x => string.Equals(x, arg, StringComparison.OrdinalIgnoreCase))
            .OrderBy(x => x);
        var startsWith = autocompleteData.Where(x => x.ToLowerInvariant().StartsWith(arg.ToLowerInvariant()))
            .OrderBy(x => x);
        var contains = autocompleteData.Where(x => x.ToLowerInvariant().Contains(arg.ToLowerInvariant()))
            .OrderBy(x => x);

        // _logger.LogInformation(JsonSerializer.Serialize(result));
        return exactMatch.Concat(startsWith.Concat(contains)).Distinct();
    }
}
