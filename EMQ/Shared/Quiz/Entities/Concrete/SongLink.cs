using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using EMQ.Shared.Core;
using EMQ.Shared.Library.Entities.Concrete;

namespace EMQ.Shared.Quiz.Entities.Concrete;

[Cloneable.Cloneable]
public partial class SongLink
{
    public static readonly int[] FileLinkTypes = { (int)SongLinkType.Catbox, (int)SongLinkType.Self };

    public string Url { get; set; } = "";

    public SongLinkType Type { get; set; } = SongLinkType.Unknown;

    public bool IsFileLink => Type is SongLinkType.Catbox or SongLinkType.Self;

    public bool IsVideo { get; set; }

    public TimeSpan Duration { get; set; }

    public string? SubmittedBy { get; set; }

    public string Sha256 { get; set; } = "";

    public MediaAnalyserResult? AnalysisRaw { get; set; }

    public SongReport? LastUnhandledReport { get; set; }

    public SongLinkAttributes Attributes { get; set; }

    public SongLinkLineage Lineage { get; set; }

    [MaxLength(4096)]
    public string Comment { get; set; } = "";

    public override string ToString() => Url;

    public static SongLink GetShortestLink(IEnumerable<SongLink> songLinks, bool preferLong)
    {
        IEnumerable<SongLink> enumerable = songLinks.ToArray();
        if (!enumerable.Any())
        {
            return new SongLink();
        }

        return preferLong
            ? enumerable.Where(x => x.IsFileLink).OrderByDescending(x => x.Duration).First()
            : enumerable.Where(x => x.IsFileLink).OrderBy(x => x.Duration).First();
    }

    // todo: write tests for this
    public static List<SongLink> FilterSongLinks(List<SongLink> dbSongLinks, bool preferLong = false)
    {
        var res = new List<SongLink>();
        var links = dbSongLinks.Where(x => x.IsFileLink).ToList();

        SongLink? targetVideoLink;
        SongLink? targetSoundLink;
        if (preferLong)
        {
            targetVideoLink = links.OrderByDescending(x => x.Duration).FirstOrDefault(x => x.IsVideo);
            targetSoundLink = links.OrderByDescending(x => x.Duration).FirstOrDefault(x => !x.IsVideo);
        }
        else
        {
            targetVideoLink = links.OrderBy(x => x.Duration).FirstOrDefault(x => x.IsVideo);
            targetSoundLink = links.OrderBy(x => x.Duration).FirstOrDefault(x => !x.IsVideo);
        }

        var allValidVideoLinks = new List<SongLink>();
        var allValidSoundLinks = new List<SongLink>();
        foreach (SongLink songLink in links.Where(x => x.IsVideo))
        {
            bool sameDuration =
                Math.Abs(targetVideoLink!.Duration.TotalMilliseconds - songLink.Duration.TotalMilliseconds) < 500;
            if (sameDuration)
            {
                allValidVideoLinks.Add(songLink);
            }
        }

        foreach (SongLink songLink in links.Where(x => !x.IsVideo))
        {
            bool sameDuration =
                Math.Abs(targetSoundLink!.Duration.TotalMilliseconds - songLink.Duration.TotalMilliseconds) < 500;
            if (sameDuration)
            {
                allValidSoundLinks.Add(songLink);
            }
        }

        if (targetVideoLink != null && targetSoundLink != null)
        {
            bool sameDuration =
                Math.Abs(
                    targetVideoLink.Duration.TotalSeconds - targetSoundLink.Duration.TotalSeconds) <
                Constants.LinkToleranceSeconds;
            if (sameDuration)
            {
                res.AddRange(allValidVideoLinks);
                res.AddRange(allValidSoundLinks);
            }
            else
            {
                List<SongLink> targetLinks;
                if (preferLong)
                {
                    targetLinks = targetVideoLink.Duration > targetSoundLink.Duration
                        ? allValidVideoLinks
                        : allValidSoundLinks;
                }
                else
                {
                    targetLinks = targetVideoLink.Duration < targetSoundLink.Duration
                        ? allValidVideoLinks
                        : allValidSoundLinks;
                }

                res.AddRange(targetLinks);
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

[Flags]
public enum SongLinkAttributes
{
    None = 0,

    [Description("Audio edited")]
    AudioEdited = 1,

    [Description("Audio replaced")]
    AudioReplaced = 2,

    [Description("Two-pass encoding")]
    TwoPassEncoding = 4,
}

[Flags]
public enum SongLinkLineage
{
    Unknown = 0,

    [Description("Game files")]
    GameFiles = 1,

    [Description("Official download")]
    OfficialDownload = 2,

    [Description("Screen recording")]
    ScreenRecording = 4,

    [Description("Album")]
    Album = 8,

    [Description("Other (explain)")]
    Other = 1048576,
}

public class ReqEditSongLinkDetails
{
    public ReqEditSongLinkDetails(SongLink songLink, int mId)
    {
        SongLink = songLink;
        MId = mId;
    }

    [Required]
    public SongLink SongLink { get; }

    [Required]
    public int MId { get; }
}
