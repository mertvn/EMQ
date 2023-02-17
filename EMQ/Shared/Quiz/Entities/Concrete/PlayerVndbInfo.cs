using System;
using System.Collections.Generic;
using System.Linq;
using Juliet.Model.VNDBObject;

namespace EMQ.Shared.Quiz.Entities.Concrete;

public class PlayerVndbInfo
{
    public string? VndbId { get; set; }

    public string? VndbApiToken { get; set; }

    public List<Label>? Labels { get; set; }
}

public class Label
{
    public int Id { get; set; }

    public bool IsPrivate { get; set; }

    public string Name { get; set; } = "";

    public List<string> VnUrls { get; set; } = new();

    public LabelKind Kind { get; set; } = LabelKind.Maybe; // todo priority

    public static Label FromVndbLabel(VNDBLabel vndbLabel)
    {
        var label = new Label() { Id = vndbLabel.Id, IsPrivate = vndbLabel.Private, Name = vndbLabel.Label, };
        return label;
    }

    public static List<Label> MergeLabels(List<Label> currentLabels, List<Label> newLabels)
    {
        var ret = new List<Label>();
        foreach (Label newLabel in newLabels)
        {
            var match = currentLabels.Find(x => x.Id == newLabel.Id);
            if (match != null)
            {
                Console.WriteLine($"found matching label {match.Id}");
                ret.Add(match);
            }
            else
            {
                ret.Add(newLabel);
            }
        }

        return ret;
    }

    public static List<string> GetValidSourcesFromLabels(List<Label> labels)
    {
        var include = labels.Where(x => x.Kind == LabelKind.Include).ToList();
        var exclude = labels.Where(x => x.Kind == LabelKind.Exclude).ToList();

        var validSources = include.SelectMany(x => x.VnUrls).ToList();
        if (exclude.Any())
        {
            validSources = validSources.Except(exclude.SelectMany(x => x.VnUrls)).ToList();
        }

        return validSources;
    }
}

public enum LabelKind
{
    Maybe = 0,
    Include = 1,
    Exclude = -1,
}
