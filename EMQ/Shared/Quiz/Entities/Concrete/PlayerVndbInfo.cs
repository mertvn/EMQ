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
}

public enum LabelKind
{
    Maybe = 0,
    Include = 1,
    Exclude = -1,
}
