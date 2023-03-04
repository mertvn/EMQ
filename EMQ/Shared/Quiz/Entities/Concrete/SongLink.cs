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

    public static SongLink GetShortestLink(IEnumerable<SongLink> songLinks)
    {
        return songLinks.OrderBy(x => x.Duration).First();
    }

    public static List<SongLink> FilterSongLinks(List<SongLink> dbSongLinks)
    {
        // todo cleanup
        // Priorities:
        // #1 Duration (Short > Long)
        // ~~#2 Video (Video > Sound)~~ not implemented
        var res = new List<SongLink>();

        // todo don't let people insert links from another host that are not similar in length to the existing hosts
        var groups = dbSongLinks.GroupBy(x => x.Type).ToList();
        // Console.WriteLine(JsonSerializer.Serialize(groups, Utils.JsoIndented));

        foreach (IGrouping<SongLinkType, SongLink> group in groups)
        {
            // Console.WriteLine("group: ");
            // Console.WriteLine(JsonSerializer.Serialize(group, Utils.JsoIndented));

            var videoLink = group.FirstOrDefault(x => x.IsVideo);
            var soundLink = group.FirstOrDefault(x => !x.IsVideo);

            if (videoLink != null && soundLink != null)
            {
                bool sameDuration =
                    Math.Abs(
                        videoLink.Duration.TotalSeconds - soundLink.Duration.TotalSeconds) < Constants.LinkToleranceSeconds;
                // Console.WriteLine(new { videoLink.Length.TotalSeconds });
                // Console.WriteLine(new { soundLink.Length.TotalSeconds });
                // Console.WriteLine(new { sameDuration });

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

        // Console.WriteLine("res: ");
        // Console.WriteLine(JsonSerializer.Serialize(res, Utils.JsoIndented));

        return res;
    }
}

public enum SongLinkType
{
    Unknown,
    Catbox,
}
