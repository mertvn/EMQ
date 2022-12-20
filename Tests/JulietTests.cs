using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Juliet.Model.Param;
using Juliet.Model.VNDBObject.Fields;
using NUnit.Framework;

namespace Tests;

public class JulietTests
{
    [SetUp]
    public void Setup()
    {
    }

    [Test]
    public async Task Test_GET_user()
    {
        var res = await Juliet.Api.GET_user(new Param() { User = "u55140" });
        Console.WriteLine(JsonSerializer.Serialize(res));

        Assert.That(res!.Single().Value.Username == "rampaa");
    }

    [Test]
    public async Task Test_GET_authinfo()
    {
        var res = await Juliet.Api.GET_authinfo(new Param() { APIToken = "" });
        Console.WriteLine(JsonSerializer.Serialize(res));
    }

    [Test]
    public async Task Test_POST_ulist()
    {
        var res = await Juliet.Api.POST_ulist(new ParamPOST_ulist()
        {
            User = "u101804",
            Exhaust = false,
            ResultsPerPage = 5,
            Fields = new List<FieldPOST_ulist>() { FieldPOST_ulist.Vote, FieldPOST_ulist.Added },
            APIToken = "",
        });
        Console.WriteLine(JsonSerializer.Serialize(res));

        Assert.That(res!.Count > 4);
    }
}
