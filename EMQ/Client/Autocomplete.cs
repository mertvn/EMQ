using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using EMQ.Shared.Library.Entities.Concrete;
using EMQ.Shared.Quiz.Entities.Concrete;

namespace EMQ.Client;

public static class Autocomplete
{
    public static IEnumerable<string> SearchAutocompleteMst(string[] data, string arg)
    {
        // todo prefer Japanese latin titles
        var startsWith = data.Where(x => x.ToLowerInvariant().StartsWith(arg.ToLowerInvariant())).OrderBy(x => x);
        var contains = data.Where(x => x.ToLowerInvariant().Contains(arg.ToLowerInvariant())).OrderBy(x => x);

        string[] final = (startsWith.Concat(contains)).Distinct().ToArray();
        // Console.WriteLine(JsonSerializer.Serialize(final));
        return final.Any() ? final.Take(25) : Array.Empty<string>();
    }

    public static IEnumerable<SongSourceCategory> SearchAutocompleteC(SongSourceCategory[] data, string arg)
    {
        // todo prefer Japanese latin titles
        var startsWith = data.Where(x => x.Name.ToLowerInvariant().StartsWith(arg.ToLowerInvariant())).OrderBy(x => x);
        var contains = data.Where(x => x.Name.ToLowerInvariant().Contains(arg.ToLowerInvariant())).OrderBy(x => x);

        var startsWith1 = data.Where(x => x.VndbId!.ToLowerInvariant().StartsWith(arg.ToLowerInvariant()))
            .OrderBy(x => x);
        var contains1 = data.Where(x => x.VndbId!.ToLowerInvariant().Contains(arg.ToLowerInvariant())).OrderBy(x => x);

        var final = startsWith.Concat(startsWith1).Concat(contains).Concat(contains1).Distinct().ToArray();
        // Console.WriteLine(JsonSerializer.Serialize(final));
        return final.Any() ? final.Take(25) : Array.Empty<SongSourceCategory>();
    }

    public static IEnumerable<AutocompleteA> SearchAutocompleteA(AutocompleteA[] data, string arg)
    {
        var startsWith = data.Where(x => x.aaLatinAlias.ToLowerInvariant().StartsWith(arg.ToLowerInvariant()))
            .OrderBy(x => x.aaLatinAlias);
        var contains = data.Where(x => x.aaLatinAlias.ToLowerInvariant().Contains(arg.ToLowerInvariant()))
            .OrderBy(x => x.aaLatinAlias);

        var startsWith1 = data.Where(x => x.aaNonLatinAlias.ToLowerInvariant().StartsWith(arg.ToLowerInvariant()))
            .OrderBy(x => x.aaNonLatinAlias);
        var contains1 = data.Where(x => x.aaNonLatinAlias.ToLowerInvariant().Contains(arg.ToLowerInvariant()))
            .OrderBy(x => x.aaNonLatinAlias);

        var final = startsWith.Concat(startsWith1).Concat(contains).Concat(contains1).Distinct().ToArray();
        // Console.WriteLine(JsonSerializer.Serialize(final));
        return final.Any() ? final.Take(25) : Array.Empty<AutocompleteA>();
    }
}
