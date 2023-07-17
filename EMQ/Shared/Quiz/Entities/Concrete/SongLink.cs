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
        // Priorities:
        // #1 Duration (Short > Long)
        // ~~#2 Video (Video > Sound)~~ not implemented
        var res = new List<SongLink>();

        // todo don't let people insert links from another host that are not similar in length to the existing hosts
        var groups = dbSongLinks.GroupBy(x => x.Type).ToList();

        foreach (IGrouping<SongLinkType, SongLink> group in groups)
        {
            var videoLink = group.FirstOrDefault(x => x.IsVideo);
            var soundLink = group.FirstOrDefault(x => !x.IsVideo);

            if (videoLink != null && soundLink != null)
            {
                bool sameDuration =
                    Math.Abs(
                        videoLink.Duration.TotalSeconds - soundLink.Duration.TotalSeconds) <
                    Constants.LinkToleranceSeconds;

                if (sameDuration)
                {
                    res.Add(videoLink);
                    res.Add(soundLink);
                }
                else
                {
                    // Priority #1
                    SongLink shortest = videoLink.Duration.TotalSeconds < soundLink.Duration.TotalSeconds
                        ? videoLink
                        : soundLink;
                    res.Add(shortest);
                }
            }
            else
            {
                res.Add((videoLink ?? soundLink)!);
            }
        }

        return res;
    }
}

public enum SongLinkType
{
    Unknown,
    Catbox,
}
