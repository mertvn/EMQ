using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
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
}
