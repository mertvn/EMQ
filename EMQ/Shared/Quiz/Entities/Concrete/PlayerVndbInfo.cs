using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Reflection;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using EMQ.Shared.Core;
using Juliet.Model.VNDBObject;

namespace EMQ.Shared.Quiz.Entities.Concrete;

public class PlayerVndbInfo
{
    [CustomValidation(typeof(PlayerVndbInfo), nameof(ValidateVndbId))]
    public string? VndbId { get; set; }

    [JsonIgnore]
    public string? VndbApiToken { get; set; } // do not use from the Client!

    public List<Label>? Labels { get; set; }

    public UserListDatabaseKind DatabaseKind { get; set; }

    public static ValidationResult ValidateVndbId(string vndbId, ValidationContext validationContext)
    {
        if (!string.IsNullOrEmpty(vndbId))
        {
            PropertyInfo databaseKindProp = validationContext.ObjectType.GetProperty(nameof(DatabaseKind))!;
            UserListDatabaseKind databaseKind =
                (UserListDatabaseKind)databaseKindProp.GetValue(validationContext.ObjectInstance, null)!;
            switch (databaseKind)
            {
                case UserListDatabaseKind.VNDB:
                    if (!Regex.IsMatch(vndbId, RegexPatterns.VndbIdRegex))
                    {
                        return new ValidationResult("Invalid VNDB Id: make sure it looks like u1234567",
                            new[] { validationContext.MemberName! });
                    }

                    break;
                case UserListDatabaseKind.EMQ:
                    break;
                case UserListDatabaseKind.MAL:
                    if (!Regex.IsMatch(vndbId, RegexPatterns.UsernameRegex))
                    {
                        return new ValidationResult("Invalid MyAnimeList username",
                            new[] { validationContext.MemberName! });
                    }

                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        return ValidationResult.Success!;
    }
}

public enum UserListDatabaseKind
{
    VNDB,
    EMQ,

    // [Description("MyAnimeList")]
    MAL,
}

public class Label
{
    public int Id { get; set; }

    public bool IsPrivate { get; set; }

    public string Name { get; set; } = "";

    public Dictionary<string, int> VNs { get; set; } = new();

    public LabelKind Kind { get; set; } = LabelKind.Maybe;

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

        var validSources = include.SelectMany(x => x.VNs.Keys).ToList();
        if (exclude.Any())
        {
            validSources = validSources.Except(exclude.SelectMany(x => x.VNs.Keys)).ToList();
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
