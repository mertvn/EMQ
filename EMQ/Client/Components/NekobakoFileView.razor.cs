using System.Linq;
using EMQ.Shared.Core.SharedDbEntities;
using Microsoft.AspNetCore.Components;

namespace EMQ.Client.Components;

public partial class NekobakoFileView
{
    [Parameter]
    public IQueryable<UserNekobako>? Items { get; set; }

    [Parameter]
    public RenderFragment? GridContent { get; set; }

    private bool ShowThumbnails { get; set; }

    private static bool IsImage(UserNekobako file)
    {
        string extension = file.extension.TrimStart('.').ToLowerInvariant();
        return extension is "webp";
    }

    private static string GetDisplayExtension(UserNekobako file)
    {
        string extension = file.extension.TrimStart('.');
        return string.IsNullOrWhiteSpace(extension) ? "FILE" : extension.ToUpperInvariant();
    }

    private static string FormatSize(long bytes)
    {
        return bytes switch
        {
            < 1024 => $"{bytes} B",
            < 1024 * 1024 => $"{bytes / 1024d:0.#} KB",
            < 1024L * 1024 * 1024 => $"{bytes / 1024d / 1024d:0.#} MB",
            _ => $"{bytes / 1024d / 1024d / 1024d:0.#} GB"
        };
    }
}
