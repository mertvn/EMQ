using System.Collections.Generic;
using EMQ.Shared.Quiz.Entities.Concrete;
using Microsoft.AspNetCore.Components.Forms;

namespace EMQ.Client;

public class UploadResult
{
    public bool Uploaded { get; set; }

    public string? FileName { get; set; }

    public string? ResultUrl { get; set; }

    public string ErrorStr { get; set; } = "";

    public List<Song> PossibleMatches { get; set; } = new();

    public Song? ChosenMatch { get; set; }

    public IBrowserFile? File { get; set; } = null;

    public string Title { get; set; } = "";

    public List<string> Artists { get; set; } = new();
}
