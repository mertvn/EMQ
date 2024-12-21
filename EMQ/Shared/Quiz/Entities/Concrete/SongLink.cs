using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using EMQ.Shared.Core;
using EMQ.Shared.Library.Entities.Concrete;

namespace EMQ.Shared.Quiz.Entities.Concrete;

public class SongLink
{
    public string Url { get; set; } = "";

    public SongLinkType Type { get; set; } = SongLinkType.Unknown;

    public static readonly int[] FileLinkTypes = { (int)SongLinkType.Catbox, (int)SongLinkType.Self };

    public bool IsFileLink => Type is SongLinkType.Catbox or SongLinkType.Self;

    public bool IsVideo { get; set; }

    public TimeSpan Duration { get; set; }

    public string? SubmittedBy { get; set; }

    public string Sha256 { get; set; } = "";

    public MediaAnalyserResult? AnalysisRaw { get; set; }

    public SongReport? LastUnhandledReport { get; set; }

    public static SongLink GetShortestLink(IEnumerable<SongLink> songLinks)
    {
        IEnumerable<SongLink> enumerable = songLinks.ToArray();
        if (!enumerable.Any())
        {
            return new SongLink();
        }

        return enumerable.Where(x => x.IsFileLink).OrderBy(x => x.Duration).First();
    }

    // todo: write tests for this
    public static List<SongLink> FilterSongLinks(List<SongLink> dbSongLinks)
    {
        var res = new List<SongLink>();

        var links = dbSongLinks.Where(x => x.IsFileLink).ToList();
        var shortestVideoLink = links.OrderBy(x => x.Duration).FirstOrDefault(x => x.IsVideo);
        var shortestSoundLink = links.OrderBy(x => x.Duration).FirstOrDefault(x => !x.IsVideo);

        var allValidVideoLinks = new List<SongLink>();
        var allValidSoundLinks = new List<SongLink>();
        foreach (SongLink songLink in links.Where(x => x.IsVideo))
        {
            bool sameDuration =
                Math.Abs(shortestVideoLink!.Duration.TotalMilliseconds - songLink.Duration.TotalMilliseconds) < 500;

            if (sameDuration)
            {
                allValidVideoLinks.Add(songLink);
            }
        }

        foreach (SongLink songLink in links.Where(x => !x.IsVideo))
        {
            bool sameDuration =
                Math.Abs(shortestSoundLink!.Duration.TotalMilliseconds - songLink.Duration.TotalMilliseconds) < 500;

            if (sameDuration)
            {
                allValidSoundLinks.Add(songLink);
            }
        }

        if (shortestVideoLink != null && shortestSoundLink != null)
        {
            bool sameDuration =
                Math.Abs(
                    shortestVideoLink.Duration.TotalSeconds - shortestSoundLink.Duration.TotalSeconds) <
                Constants.LinkToleranceSeconds;

            if (sameDuration)
            {
                res.AddRange(allValidVideoLinks);
                res.AddRange(allValidSoundLinks);
            }
            else
            {
                List<SongLink> shortest =
                    shortestVideoLink.Duration.TotalSeconds < shortestSoundLink.Duration.TotalSeconds
                        ? allValidVideoLinks
                        : allValidSoundLinks;
                res.AddRange(shortest);
            }
        }
        else
        {
            res.AddRange(allValidVideoLinks.Any() ? allValidVideoLinks : allValidSoundLinks);
        }

        // we randomize links here to allow different video links to play, because NextSong just takes the first link it finds
        res = res.Shuffle().Concat(dbSongLinks.Where(x => !x.IsFileLink)).ToList();
        return res;
    }
}

// todo descriptions
public enum SongLinkType
{
    Unknown,
    Catbox,
    Self,

    [Description("MusicBrainz recording")]
    MusicBrainzRecording, // todo make bgm uploads use this instead of the current rigmarole

    [Description("ErogameScape music")]
    ErogameScapeMusic,

    [Description("Anison.info song")]
    AnisonInfoSong,

    [Description("Wikidata item")]
    WikidataItem,

    [Description("AniDB song")]
    AniDBSong,
}
