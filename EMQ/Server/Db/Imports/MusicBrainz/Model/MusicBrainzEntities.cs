using System;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

// ReSharper disable IdentifierTypo
// ReSharper disable InconsistentNaming

namespace EMQ.Server.Db.Imports.MusicBrainz.Model;

public class MusicBrainzJson
{
    public aaa_rids aaa_rids { get; set; } = new();

    public release release { get; set; } = new();

    public medium medium { get; set; } = new();

    public track track { get; set; } = new();

    public release_group release_group { get; set; } = new();

    public artist_credit artist_credit { get; set; } = new();

    public recording recording { get; set; } = new();

    [JsonPropertyName("json_agg_acn")]
    public artist_credit_name[] artist_credit_name { get; set; } = Array.Empty<artist_credit_name>();

    [JsonPropertyName("json_agg_a")]
    public artist[] artist { get; set; } = Array.Empty<artist>();
}

public class aaa_rids
{
    public int? rid { get; set; }
    public int? rgid { get; set; }
    public string? vgmdburl { get; set; }
    public string? vndbid { get; set; }
}

public class release
{
    [Required]
    public int id { get; set; }

    [Required]
    public Guid gid { get; set; }

    [Required]
    public string name { get; set; } = "";

    [Required]
    public int artist_credit { get; set; }

    [Required]
    public int release_group { get; set; }

    public int? status { get; set; }
    public int? packaging { get; set; }
    public int? language { get; set; }
    public int? script { get; set; }
    public string? barcode { get; set; }

    [Required]
    public string comment { get; set; } = "";

    [Required]
    public int edits_pending { get; set; }

    [Required]
    public short quality { get; set; }

    public DateTime? last_updated { get; set; }
}

public class medium
{
    [Required]
    public int id { get; set; }

    [Required]
    public int release { get; set; }

    [Required]
    public int position { get; set; }

    public int? format { get; set; }

    [Required]
    public string name { get; set; } = "";

    [Required]
    public int edits_pending { get; set; }

    public DateTime? last_updated { get; set; }

    [Required]
    public int track_count { get; set; }
}

public class track
{
    [Required]
    public int id { get; set; }

    [Required]
    public Guid gid { get; set; }

    [Required]
    public int recording { get; set; }

    [Required]
    public int medium { get; set; }

    [Required]
    public int position { get; set; }

    [Required]
    public string number { get; set; } = "";

    [Required]
    public string name { get; set; } = "";

    [Required]
    public int artist_credit { get; set; }

    public int? length { get; set; }

    [Required]
    public int edits_pending { get; set; }

    public DateTime? last_updated { get; set; }

    [Required]
    public bool is_data_track { get; set; }
}

public class release_group
{
    [Required]
    public int id { get; set; }

    [Required]
    public Guid gid { get; set; }

    [Required]
    public string name { get; set; } = "";

    [Required]
    public int artist_credit { get; set; }

    public int? type { get; set; }

    [Required]
    public string comment { get; set; } = "";

    [Required]
    public int edits_pending { get; set; }

    public DateTime? last_updated { get; set; }
}

public class artist_credit
{
    [Required]
    public int id { get; set; }

    [Required]
    public string name { get; set; } = "";

    [Required]
    public short artist_count { get; set; }

    public int? ref_count { get; set; }
    public DateTime? created { get; set; }

    [Required]
    public int edits_pending { get; set; }

    [Required]
    public Guid gid { get; set; }
}

public class recording
{
    [Required]
    public int id { get; set; }

    [Required]
    public Guid gid { get; set; }

    [Required]
    public string name { get; set; } = "";

    [Required]
    public int artist_credit { get; set; }

    public int? length { get; set; }

    [Required]
    public string comment { get; set; } = "";

    [Required]
    public int edits_pending { get; set; }

    public DateTime? last_updated { get; set; }

    [Required]
    public bool video { get; set; }
}

public class artist_credit_name
{
    [Required]
    public int artist_credit { get; set; }

    [Required]
    public short position { get; set; }

    [Required]
    public int artist { get; set; }

    [Required]
    public string name { get; set; } = "";

    [Required]
    public string join_phrase { get; set; } = "";
}

public class artist
{
    [Required]
    public int id { get; set; }

    [Required]
    public Guid gid { get; set; }

    [Required]
    public string name { get; set; } = "";

    [Required]
    public string sort_name { get; set; } = "";

    public short? begin_date_year { get; set; }
    public short? begin_date_month { get; set; }
    public short? begin_date_day { get; set; }
    public short? end_date_year { get; set; }
    public short? end_date_month { get; set; }
    public short? end_date_day { get; set; }
    public int? type { get; set; }
    public int? area { get; set; }
    public int? gender { get; set; }

    [Required]
    public string comment { get; set; } = "";

    [Required]
    public int edits_pending { get; set; }

    public DateTime? last_updated { get; set; }

    [Required]
    public bool ended { get; set; }

    public int? begin_area { get; set; }
    public int? end_area { get; set; }
}
