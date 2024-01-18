namespace EMQ.Shared.Core;

public class UploadResult
{
    public bool Uploaded { get; set; }

    public string? FileName { get; set; }

    public string? ResultUrl { get; set; }

    public string ErrorStr { get; set; } = "";
}
