using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

// ReSharper disable CollectionNeverUpdated.Global
// ReSharper disable IdentifierTypo
// ReSharper disable InconsistentNaming

namespace EMQ.Shared.Core;

public class MBArea
{
    [JsonPropertyName("type")]
    public object? type { get; set; }

    [JsonPropertyName("sort-name")]
    public string sortname { get; set; } = "";

    [JsonPropertyName("id")]
    public Guid? id { get; set; }

    [JsonPropertyName("iso-3166-1-codes")]
    public List<string> iso31661codes { get; set; } = new();

    [JsonPropertyName("type-id")]
    public object? typeid { get; set; }

    [JsonPropertyName("name")]
    public string name { get; set; } = "";

    [JsonPropertyName("disambiguation")]
    public string disambiguation { get; set; } = "";
}

public class MBArtist
{
    [JsonPropertyName("disambiguation")]
    public string disambiguation { get; set; } = "";

    [JsonPropertyName("type-id")]
    public string typeid { get; set; } = "";

    [JsonPropertyName("name")]
    public string name { get; set; } = "";

    [JsonPropertyName("id")]
    public Guid? id { get; set; }

    [JsonPropertyName("type")]
    public string type { get; set; } = "";

    [JsonPropertyName("sort-name")]
    public string sortname { get; set; } = "";
}

public class MBArtistCredit
{
    [JsonPropertyName("artist")]
    public MBArtist? artist { get; set; }

    [JsonPropertyName("joinphrase")]
    public string joinphrase { get; set; } = "";

    [JsonPropertyName("name")]
    public string name { get; set; } = "";
}

public class MBCoverArtArchive
{
    [JsonPropertyName("back")]
    public bool back { get; set; }

    [JsonPropertyName("count")]
    public int count { get; set; }

    [JsonPropertyName("artwork")]
    public bool artwork { get; set; }

    [JsonPropertyName("front")]
    public bool front { get; set; }

    [JsonPropertyName("darkened")]
    public bool darkened { get; set; }
}

public class MBLabel
{
    [JsonPropertyName("id")]
    public Guid? id { get; set; }

    [JsonPropertyName("type")]
    public string type { get; set; } = "";

    [JsonPropertyName("sort-name")]
    public string sortname { get; set; } = "";

    [JsonPropertyName("disambiguation")]
    public string disambiguation { get; set; } = "";

    [JsonPropertyName("name")]
    public string name { get; set; } = "";

    [JsonPropertyName("label-code")]
    public object? labelcode { get; set; }

    [JsonPropertyName("type-id")]
    public string typeid { get; set; } = "";
}

public class MBLabelInfo
{
    [JsonPropertyName("catalog-number")]
    public string catalognumber { get; set; } = "";

    [JsonPropertyName("label")]
    public MBLabel? label { get; set; }
}

public class MBMedium
{
    [JsonPropertyName("title")]
    public string title { get; set; } = "";

    [JsonPropertyName("tracks")]
    public List<MBTrack> tracks { get; set; } = new();

    [JsonPropertyName("format")]
    public string format { get; set; } = "";

    [JsonPropertyName("track-count")]
    public int trackcount { get; set; }

    [JsonPropertyName("position")]
    public int position { get; set; }

    [JsonPropertyName("format-id")]
    public string formatid { get; set; } = "";

    [JsonPropertyName("track-offset")]
    public int trackoffset { get; set; }
}

public class MBReleaseRecording
{
    [JsonPropertyName("video")]
    public bool video { get; set; }

    [JsonPropertyName("disambiguation")]
    public string disambiguation { get; set; } = "";

    [JsonPropertyName("first-release-date")]
    public string firstreleasedate { get; set; } = "";

    [JsonPropertyName("length")]
    public int length { get; set; }

    [JsonPropertyName("id")]
    public Guid? id { get; set; }

    [JsonPropertyName("title")]
    public string title { get; set; } = "";
}

public class MBReleaseEvent
{
    [JsonPropertyName("area")]
    public MBArea? area { get; set; }

    [JsonPropertyName("date")]
    public string date { get; set; } = "";
}

public class MBReleaseGroup
{
    [JsonPropertyName("first-release-date")]
    public string firstreleasedate { get; set; } = "";

    [JsonPropertyName("title")]
    public string title { get; set; } = "";

    [JsonPropertyName("id")]
    public Guid? id { get; set; }

    [JsonPropertyName("secondary-type-ids")]
    public List<object?> secondarytypeids { get; set; } = new();

    [JsonPropertyName("primary-type-id")]
    public string primarytypeid { get; set; } = "";

    [JsonPropertyName("secondary-types")]
    public List<object?> secondarytypes { get; set; } = new();

    [JsonPropertyName("primary-type")]
    public string primarytype { get; set; } = "";

    [JsonPropertyName("disambiguation")]
    public string disambiguation { get; set; } = "";
}

public class MBRelease
{
    [JsonPropertyName("text-representation")]
    public MBTextRepresentation? textrepresentation { get; set; }

    [JsonPropertyName("country")]
    public string country { get; set; } = "";

    [JsonPropertyName("date")]
    public string date { get; set; } = "";

    [JsonPropertyName("packaging-id")]
    public object? packagingid { get; set; }

    [JsonPropertyName("status-id")]
    public string statusid { get; set; } = "";

    [JsonPropertyName("disambiguation")]
    public string disambiguation { get; set; } = "";

    [JsonPropertyName("status")]
    public string status { get; set; } = "";

    [JsonPropertyName("asin")]
    public object? asin { get; set; }

    [JsonPropertyName("quality")]
    public string quality { get; set; } = "";

    [JsonPropertyName("label-info")]
    public List<MBLabelInfo> labelinfo { get; set; } = new();

    [JsonPropertyName("release-events")]
    public List<MBReleaseEvent> releaseevents { get; set; } = new();

    [JsonPropertyName("artist-credit")]
    public List<MBArtistCredit> artistcredit { get; set; } = new();

    [JsonPropertyName("cover-art-archive")]
    public MBCoverArtArchive? coverartarchive { get; set; }

    [JsonPropertyName("id")]
    public Guid? id { get; set; }

    [JsonPropertyName("title")]
    public string title { get; set; } = "";

    [JsonPropertyName("packaging")]
    public object? packaging { get; set; }

    [JsonPropertyName("barcode")]
    public string barcode { get; set; } = "";

    [JsonPropertyName("release-group")]
    public MBReleaseGroup? releasegroup { get; set; }

    [JsonPropertyName("media")]
    public List<MBMedium> media { get; set; } = new();
}

public class MBTextRepresentation
{
    [JsonPropertyName("script")]
    public string script { get; set; } = "";

    [JsonPropertyName("language")]
    public string language { get; set; } = "";
}

public class MBTrack
{
    [JsonPropertyName("id")]
    public Guid? id { get; set; }

    [JsonPropertyName("number")]
    public string number { get; set; } = "";

    [JsonPropertyName("title")]
    public string title { get; set; } = "";

    [JsonPropertyName("length")]
    public int length { get; set; }

    [JsonPropertyName("recording")]
    public MBRecording? recording { get; set; }

    [JsonPropertyName("position")]
    public int position { get; set; }
}
