using System;
using System.Collections.Generic;

// ReSharper disable CollectionNeverUpdated.Global
// ReSharper disable ClassNeverInstantiated.Global
// ReSharper disable IdentifierTypo
// ReSharper disable InconsistentNaming
// ReSharper disable UnusedAutoPropertyAccessor.Global

namespace EMQ.Shared.Core;

public class JikanAired
{
    public DateTime? from { get; set; }
    public DateTime? to { get; set; }
    public JikanProp? prop { get; set; }
    public string? @string { get; set; }
}

public class JikanBroadcast
{
    public string? day { get; set; }
    public string? time { get; set; }
    public string? timezone { get; set; }
    public string? @string { get; set; }
}

public class JikanData
{
    public int? mal_id { get; set; }
    public string? url { get; set; }
    public bool? approved { get; set; }
    public List<JikanTitle>? titles { get; set; }
    public string? title { get; set; }
    public string? title_english { get; set; }
    public string? title_japanese { get; set; }
    public List<object>? title_synonyms { get; set; }
    public string? type { get; set; }
    public string? source { get; set; }
    public int? episodes { get; set; }
    public string? status { get; set; }
    public bool? airing { get; set; }
    public JikanAired? aired { get; set; }
    public string? duration { get; set; }
    public string? rating { get; set; }
    public double? score { get; set; }
    public int? scored_by { get; set; }
    public int? rank { get; set; }
    public int? popularity { get; set; }
    public int? members { get; set; }
    public int? favorites { get; set; }
    public string? synopsis { get; set; }
    public string? background { get; set; }
    public string? season { get; set; }
    public int? year { get; set; }
    public JikanBroadcast? broadcast { get; set; }
    public List<JikanProducer>? producers { get; set; }
    public List<JikanStudio>? studios { get; set; }
    public List<JikanGenre>? genres { get; set; }
    public List<object>? explicit_genres { get; set; }
    public List<JikanTheme>? themes { get; set; }
    public List<object>? demographics { get; set; }
}

public class JikanFromTo
{
    public int? day { get; set; }
    public int? month { get; set; }
    public int? year { get; set; }
}

public class JikanGenre
{
    public int? mal_id { get; set; }
    public string? type { get; set; }
    public string? name { get; set; }
    public string? url { get; set; }
}

public class JikanProducer
{
    public int? mal_id { get; set; }
    public string? type { get; set; }
    public string? name { get; set; }
    public string? url { get; set; }
}

public class JikanProp
{
    public JikanFromTo? from { get; set; }
    public JikanFromTo? to { get; set; }
}

public class JikanRoot
{
    public JikanData? data { get; set; }
}

public class JikanStudio
{
    public int? mal_id { get; set; }
    public string? type { get; set; }
    public string? name { get; set; }
    public string? url { get; set; }
}

public class JikanTheme
{
    public int? mal_id { get; set; }
    public string? type { get; set; }
    public string? name { get; set; }
    public string? url { get; set; }
}

public class JikanTitle
{
    public string? type { get; set; }
    public string? title { get; set; }
}
