using System;
using System.Collections.Generic;
using System.Linq;
using EMQ.Shared.Core;

namespace EMQ.Shared.Quiz.Entities.Concrete;

public class SongLink
{
    public string Url { get; set; } = "";

    public SongLinkType Type { get; set; } = SongLinkType.Unknown;

    public bool IsVideo { get; set; }

    public TimeSpan Duration { get; set; }

    public string? SubmittedBy { get; set; }

    // todo add total_bytes

    public static SongLink GetShortestLink(IEnumerable<SongLink> songLinks)
    {
        IEnumerable<SongLink> enumerable = songLinks.ToArray();
        if (!enumerable.Any())
        {
            return new SongLink();
        }

        return enumerable.OrderBy(x => x.Duration).First();
    }

    // todo: write tests for this
    public static List<SongLink> FilterSongLinks(List<SongLink> dbSongLinks)
    {
        var res = new List<SongLink>();

        var shortestVideoLink = dbSongLinks.OrderBy(x => x.Duration).FirstOrDefault(x => x.IsVideo);
        var shortestSoundLink = dbSongLinks.OrderBy(x => x.Duration).FirstOrDefault(x => !x.IsVideo);

        var allValidVideoLinks = new List<SongLink>();
        foreach (SongLink songLink in dbSongLinks.Where(x => x.IsVideo))
        {
            bool sameDuration =
                Math.Abs(shortestVideoLink!.Duration.TotalMilliseconds - songLink.Duration.TotalMilliseconds) < 500;

            if (sameDuration)
            {
                allValidVideoLinks.Add(songLink);
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
                res.Add(shortestSoundLink);
            }
            else
            {
                List<SongLink> shortest =
                    shortestVideoLink.Duration.TotalSeconds < shortestSoundLink.Duration.TotalSeconds
                        ? allValidVideoLinks
                        : new List<SongLink> { shortestSoundLink };
                res.AddRange(shortest);
            }
        }
        else
        {
            if (allValidVideoLinks.Any())
            {
                res.AddRange(allValidVideoLinks);
            }
            else
            {
                res.Add(shortestSoundLink!);
            }
        }

        // we randomize links here to allow different video links to play, because NextSong just takes the first link it finds
        res = res.OrderBy(x => Random.Shared.Next()).ToList();
        return res;
    }
}

public enum SongLinkType
{
    Unknown,
    Catbox,
    Self, // todo
}
