﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using EMQ.Shared.Core;
using EMQ.Shared.Library.Entities.Concrete;
using EMQ.Shared.Quiz.Entities.Concrete;

namespace EMQ.Client;

public static class Autocomplete
{
    // todo search exact match on unnormalized input first for e.g. ONE.
    // todo build a (trigram) index and search that
    public static IEnumerable<string> SearchAutocompleteMst(AutocompleteMst[] data, string arg)
    {
        // todo prefer Japanese latin titles
        arg = arg.NormalizeForAutocomplete();
        // Console.WriteLine(arg);

        if (arg == "")
        {
            return Array.Empty<string>();
        }

        var startsWith = data.Where(x =>
                x.MSTLatinTitleNormalized.StartsWith(arg) || x.MSTNonLatinTitleNormalized.StartsWith(arg))
            .OrderBy(x => x.MSTLatinTitle)
            .ToArray();

        var contains = data.Where(x =>
                x.MSTLatinTitleNormalized.Contains(arg) || x.MSTNonLatinTitleNormalized.Contains(arg))
            .OrderBy(x => x.MSTLatinTitle)
            .ToArray();

        var startsWithLT = startsWith.Select(x => x.MSTLatinTitle);
        var startsWithNLT = startsWith.Select(x => x.MSTNonLatinTitle);

        var containsLT = contains.Select(x => x.MSTLatinTitle);
        var containsNLT = contains.Select(x => x.MSTNonLatinTitle);

        string[] final = startsWithLT.Concat(containsLT)
            .Concat(startsWithNLT).Concat(containsNLT)
            .Distinct()
            .Where(x => x != "").ToArray();
        // Console.WriteLine(JsonSerializer.Serialize(final));
        return final.Any() ? final.Take(25) : Array.Empty<string>();
    }

    public static IEnumerable<SongSourceCategory> SearchAutocompleteC(SongSourceCategory[] data, string arg)
    {
        arg = arg.Trim();
        if (arg == "")
        {
            return Array.Empty<SongSourceCategory>();
        }

        // todo prefer Japanese latin titles
        var startsWith = data.Where(x => x.Name.ToLowerInvariant().StartsWith(arg.ToLowerInvariant()))
            .OrderBy(x => x.Name);
        var contains = data.Where(x => x.Name.ToLowerInvariant().Contains(arg.ToLowerInvariant()))
            .OrderBy(x => x.Name);

        var startsWith1 = data.Where(x => x.VndbId?.ToLowerInvariant().StartsWith(arg.ToLowerInvariant()) ?? false)
            .OrderBy(x => x.VndbId);
        var contains1 = data.Where(x => x.VndbId?.ToLowerInvariant().Contains(arg.ToLowerInvariant()) ?? false)
            .OrderBy(x => x.VndbId);

        var final = startsWith.Concat(startsWith1).Concat(contains).Concat(contains1).Distinct().ToArray();
        // Console.WriteLine(JsonSerializer.Serialize(final));
        return final.Any() ? final.Take(25) : Array.Empty<SongSourceCategory>();
    }

    public static IEnumerable<AutocompleteA> SearchAutocompleteA(AutocompleteA[] data, string arg)
    {
        arg = arg.Trim();
        if (arg == "")
        {
            return Array.Empty<AutocompleteA>();
        }

        var startsWith = data.Where(x => x.AALatinAlias.ToLowerInvariant().StartsWith(arg.ToLowerInvariant()))
            .OrderBy(x => x.AALatinAlias);
        var contains = data.Where(x => x.AALatinAlias.ToLowerInvariant().Contains(arg.ToLowerInvariant()))
            .OrderBy(x => x.AALatinAlias);

        var startsWith1 = data.Where(x => x.AANonLatinAlias.ToLowerInvariant().StartsWith(arg.ToLowerInvariant()))
            .OrderBy(x => x.AANonLatinAlias);
        var contains1 = data.Where(x => x.AANonLatinAlias.ToLowerInvariant().Contains(arg.ToLowerInvariant()))
            .OrderBy(x => x.AANonLatinAlias);

        var final = startsWith.Concat(startsWith1).Concat(contains).Concat(contains1).Distinct().ToArray();
        // Console.WriteLine(JsonSerializer.Serialize(final));
        return final.Any() ? final.Take(25) : Array.Empty<AutocompleteA>();
    }

    public static IEnumerable<string> SearchAutocompleteMt(AutocompleteMt[] data, string arg)
    {
        arg = arg.NormalizeForAutocomplete();
        // Console.WriteLine(arg);

        if (arg == "")
        {
            return Array.Empty<string>();
        }

        var startsWith = data.Where(x =>
                x.MTLatinTitleNormalized.StartsWith(arg))
            .OrderBy(x => x.MTLatinTitle)
            .ToArray();

        var contains = data.Where(x =>
                x.MTLatinTitleNormalized.Contains(arg))
            .OrderBy(x => x.MTLatinTitle)
            .ToArray();

        var startsWithLT = startsWith.Select(x => x.MTLatinTitle);
        var containsLT = contains.Select(x => x.MTLatinTitle);

        string[] final = startsWithLT.Concat(containsLT)
            .Distinct()
            .ToArray();
        // Console.WriteLine(JsonSerializer.Serialize(final));
        return final.Any() ? final.Take(25) : Array.Empty<string>();
    }

    public static IEnumerable<string> SearchAutocompletePlayer(string[] data, string arg)
    {
        arg = arg.NormalizeForAutocomplete();
        // Console.WriteLine(arg);

        if (arg == "")
        {
            return Array.Empty<string>();
        }

        var startsWith = data.Where(x =>
                x.NormalizeForAutocomplete().StartsWith(arg))
            .OrderBy(x => x)
            .ToArray();

        var contains = data.Where(x =>
                x.NormalizeForAutocomplete().Contains(arg))
            .OrderBy(x => x)
            .ToArray();

        string[] final = startsWith.Concat(contains)
            .Distinct()
            .ToArray();
        // Console.WriteLine(JsonSerializer.Serialize(final));
        return final.Any() ? final.Take(25) : Array.Empty<string>();
    }
}
