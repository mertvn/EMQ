using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using EMQ.Client.Components;
using EMQ.Shared.Core;
using EMQ.Shared.Quiz.Entities.Concrete;
using EMQ.Shared.VNDB.Business;
using NUnit.Framework;

namespace Tests;

public class VndbTests
{
    [SetUp]
    public void Setup()
    {
    }

    [Test]
    public async Task Test_GrabVnsFromVndb()
    {
        PlayerVndbInfo vndbInfo = new PlayerVndbInfo()
        {
            VndbId = "u101804",
            VndbApiToken = "",
            Labels = new List<Label>
            {
                new()
                {
                    Id = 2, // Finished
                    Kind = LabelKind.Include
                },
                new()
                {
                    Id = 7, // Voted
                    Kind = LabelKind.Include
                },
                new()
                {
                    Id = 6, // Blacklist
                    Kind = LabelKind.Exclude
                },
            }
        };

        var labels = await VndbMethods.GrabPlayerVNsFromVndb(vndbInfo);
        Console.WriteLine(JsonSerializer.Serialize(labels, Utils.Jso));
        Assert.That(labels.Count > 1);
        Assert.That(labels.First().VNs.Count > 1);
    }

    [Test]
    public async Task Test_MergeLabels()
    {
        var vndbInfo = new PlayerVndbInfo()
        {
            Labels = new List<Label>()
            {
                new Label() { Id = 1, Name = "Playing", Kind = LabelKind.Maybe },
                new Label() { Id = 4, Name = "Dropped", Kind = LabelKind.Include }
            },
            VndbId = "u101804",
        };

        var comp = new PlayerPreferencesComponent();
        var labels = await comp.FetchLabelsInner(vndbInfo);
        Console.WriteLine(JsonSerializer.Serialize(labels, Utils.JsoIndented));
    }
}
