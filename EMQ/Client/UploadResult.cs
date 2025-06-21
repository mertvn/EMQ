using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using EMQ.Shared.Quiz.Entities.Concrete;
using Microsoft.AspNetCore.Components.Forms;

namespace EMQ.Client;

public class UploadResult
{
    public bool IsSuccess { get; set; }

    public string? FileName { get; set; }

    public string? ResultUrl { get; set; }

    public string? ExtractedResultUrl { get; set; }

    public string ErrorStr { get; set; } = "";

    public List<Song> PossibleMatches { get; set; } = new();

    public Song? ChosenMatch { get; set; }

    public IBrowserFile? File { get; set; } = null;

    public string Title { get; set; } = "";

    public List<string> Artists { get; set; } = new();

    public List<string> MBRecordingOrTrackIds { get; set; } = new();

    public string UploadId { get; set; } = "";

    // null: no action taken so far
    // true: currently being processed
    // false: processing has finished
    public bool? IsProcessing { get; set; }
}

public class UploadOptions
{
    [Required]
    public bool DoTwoPass { get; set; } = true;

    [Required]
    public bool ShouldCropSilence { get; set; } = true;

    [Required]
    public bool ShouldAdjustVolume { get; set; } = true;

    [Required]
    [Range(0, 30 * 60)]
    public float Ss { get; set; }

    [Required]
    [Range(0, 30 * 60)]
    public float To { get; set; }
}
