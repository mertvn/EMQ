using System;
using System.ComponentModel.DataAnnotations.Schema;

namespace Tests;

using System.Collections.Generic;

[Table("generic_fetch")]
public class GenericFetch
{
    public string key { get; set; } = "";

    public int status { get; set; }

    public DateTime modified_at { get; set; }

    public string? value { get; set; }
}

public class JikanAnimeIntermediary
{
    public string role { get; set; } = "";
    public JikanAnime anime { get; set; } = new();
}

public class JikanAnime
{
    public int? mal_id { get; set; }
    public JikanImages images { get; set; } = new();
    public string title { get; set; } = "";
}

public class JikanCharactersRootData
{
    public int? mal_id { get; set; }
    public string url { get; set; } = "";
    public JikanImages images { get; set; } = new();
    public string name { get; set; } = "";
    public string name_kanji { get; set; } = "";
    public List<string> nicknames { get; set; } = new();

    public List<JikanAnimeIntermediary> anime { get; set; } = new();

    // public List<object> manga { get; set; }
    public List<JikanVoice> voices { get; set; } = new();
}

public class JikanImages
{
    public JikanWebp webp { get; set; } = new();
}

public class JikanPerson
{
    public int? mal_id { get; set; }
    public string url { get; set; } = "";
    public JikanImages images { get; set; } = new();
    public string name { get; set; } = "";
}

public class JikanCharactersFullRoot
{
    public JikanCharactersRootData data { get; set; } = new();
}

public class JikanVoice
{
    public JikanPerson person { get; set; } = new();
    public string language { get; set; } = "";
}

public class JikanWebp
{
    public string image_url { get; set; } = "";
    public string large_image_url { get; set; } = "";
}
