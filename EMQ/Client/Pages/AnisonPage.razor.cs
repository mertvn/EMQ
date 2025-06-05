using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using EMQ.Shared.Core;

namespace EMQ.Client.Pages;

public partial class AnisonPage
{
    public string InputText { get; set; } = "Loading data, can take a few minutes.";

    public Dictionary<string, AnisonSong[]> Data { get; set; } = new();

    public List<AnisonSong> CurrentSearchResults { get; set; } = new();

    protected override async Task OnInitializedAsync()
    {
        await using var stream = await _client.GetStreamAsync("!anison_song_sn_norm_index.json.gz");
        await using var gzipStream = new GZipStream(stream, CompressionMode.Decompress);
        Data = JsonSerializer.Deserialize<Dictionary<string, AnisonSong[]>>(gzipStream)!;
        InputText = "";
    }

    public void SearchSourceTitle()
    {
        if (!string.IsNullOrWhiteSpace(InputText))
        {
            CurrentSearchResults = Data
                .Where(x => x.Value.Any(y =>
                    y.Sources.Any(z => string.Equals(z.Title, InputText, StringComparison.OrdinalIgnoreCase))))
                .SelectMany(x => x.Value.Where(y =>
                    y.Sources.Any(z => string.Equals(z.Title, InputText, StringComparison.OrdinalIgnoreCase))))
                .Take(1000).ToList();
        }
    }

    public void SearchArtistName()
    {
        if (!string.IsNullOrWhiteSpace(InputText))
        {
            CurrentSearchResults = Data
                .Where(x => x.Value.Any(y =>
                    y.Artists.Any(z => string.Equals(z.Value.Title, InputText, StringComparison.OrdinalIgnoreCase))))
                .SelectMany(x => x.Value.Where(y =>
                    y.Artists.Any(z => string.Equals(z.Value.Title, InputText, StringComparison.OrdinalIgnoreCase))))
                .Take(1000).ToList();
        }
    }

    public void SearchSongTitle()
    {
        if (!string.IsNullOrWhiteSpace(InputText))
        {
            string norm = InputText.NormalizeForAutocomplete();
            CurrentSearchResults = Data
                .Where(x => x.Key.Contains(norm))
                .SelectMany(x => x.Value).Take(1000).ToList();
        }
    }
}

public class AnisonSong
{
    public int Id { get; set; }

    public string Title { get; set; } = "";

    public Dictionary<int, AnisonSongArtist> Artists { get; set; } = new();

    public List<AnisonSongSource> Sources { get; set; } = new();
}

public class AnisonSongArtist
{
    public int Id { get; set; }

    public string Title { get; set; } = "";

    // public string Character { get; set; } = "";

    public HashSet<string> Roles { get; set; } = new();
}

public class AnisonSongSource
{
    public int Id { get; set; }

    public string Genre { get; set; } = "";

    public string Title { get; set; } = "";

    public string Type { get; set; } = "";
}
