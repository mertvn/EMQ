using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Threading.Tasks;
using Juliet.Model.Filters;
using Juliet.Model.Param;
using Juliet.Model.VNDBObject;
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
    public async Task Test_GET_ulist_labels()
    {
        var res = await Juliet.Api.GET_ulist_labels(new Param() { User = "u1" });
        Console.WriteLine(JsonSerializer.Serialize(res));

        Assert.That(res!.Labels.Length > 5);
    }

    [Test, Explicit]
    public async Task Test_PATCH_ulist()
    {
        await Juliet.Api.PATCH_ulist(new ParamPATCH_ulist() { APIToken = "", Id = "v1", labels_set = new[] { 5 } });
    }

    [Test, Explicit]
    public async Task Test_PATCH_rlist()
    {
        await Juliet.Api.PATCH_rlist(new ParamPATCH_rlist() { APIToken = "", Id = "r1", status = ListStatus.Unknown });
    }

    [Test]
    public async Task Test_POST_ulist()
    {
        var res = await Juliet.Api.POST_ulist(new ParamPOST_ulist()
        {
            User = "u101804",
            Exhaust = false,
            ResultsPerPage = 5,
            Fields = new List<FieldPOST_ulist>() { FieldPOST_ulist.LabelsId, FieldPOST_ulist.LabelsLabel },
            APIToken = "",
            Filters = null
        });
        Console.WriteLine(JsonSerializer.Serialize(res));

        Assert.That(res!.First().Results.Count > 4);
    }

    [Test]
    public async Task Test_POST_ulist_Nested()
    {
        var res = await Juliet.Api.POST_ulist(new ParamPOST_ulist()
        {
            User = "u101804",
            Exhaust = false,
            ResultsPerPage = 5,
            Fields = new List<FieldPOST_ulist>() { FieldPOST_ulist.Vote, FieldPOST_ulist.Added },
            APIToken = "",
            Filters = new Combinator(CombinatorKind.Or,
                new List<Query>
                {
                    new Combinator(CombinatorKind.Or,
                        new List<Query>
                        {
                            new Predicate(FilterField.Label, FilterOperator.Equal, 2),
                            new Predicate(FilterField.Label, FilterOperator.Equal, 6),
                        }),
                    new Combinator(CombinatorKind.Or,
                        new List<Query>
                        {
                            new Predicate(FilterField.Label, FilterOperator.Equal, 4),
                            new Predicate(FilterField.Label, FilterOperator.Equal, 6),
                        }),
                    new Combinator(CombinatorKind.Or,
                        new List<Query>
                        {
                            new Combinator(CombinatorKind.And,
                                new List<Query>
                                {
                                    new Predicate(FilterField.Label, FilterOperator.Equal, 2),
                                    new Predicate(FilterField.Label, FilterOperator.Equal, 6),
                                }),
                            new Combinator(CombinatorKind.And,
                                new List<Query>
                                {
                                    new Predicate(FilterField.Label, FilterOperator.Equal, 4),
                                    new Predicate(FilterField.Label, FilterOperator.Equal, 6),
                                })
                        })
                })
        });
        Console.WriteLine(JsonSerializer.Serialize(res));

        Assert.That(res!.First().Results.Count > 1);
    }

    [Test]
    public async Task Test_POST_vn_GetIdBySearchingTitle()
    {
        var res = await Juliet.Api.POST_vn(new ParamPOST_vn()
        {
            Fields = new List<FieldPOST_vn>() { FieldPOST_vn.Id },
            Filters = new Predicate(FilterField.Search, FilterOperator.Equal, "シャマナシャマナ ～月とこころと太陽の魔法～")
        });
        Console.WriteLine(JsonSerializer.Serialize(res,
            new JsonSerializerOptions() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping }));

        Assert.That(res!.Single().Results.Single().Id == "v540");
    }

    [Test]
    public async Task Test_POST_vn_RawFilters()
    {
        var res = await Juliet.Api.POST_vn(new ParamPOST_vn()
        {
            Fields = new List<FieldPOST_vn>() { FieldPOST_vn.Id }, RawFilters = "023gjaN3830X1o",
        });
        Console.WriteLine(JsonSerializer.Serialize(res,
            new JsonSerializerOptions() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping }));

        Assert.That(res!.Single().Results.Count > 17);
    }

    [Test]
    public async Task Test_POST_vn_GetTitles()
    {
        var res = await Juliet.Api.POST_vn(new ParamPOST_vn()
        {
            Fields = new List<FieldPOST_vn>()
            {
                FieldPOST_vn.Id, FieldPOST_vn.Title, FieldPOST_vn.AltTitle, FieldPOST_vn.Released
            },
            Filters = new Predicate(FilterField.Search, FilterOperator.Equal, "シャマナシャマナ ～月とこころと太陽の魔法～")
        });
        Console.WriteLine(JsonSerializer.Serialize(res,
            new JsonSerializerOptions() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping }));

        Assert.That(res!.Single().Results.Single().Title == "Shamana Shamana ~Tsuki to Kokoro to Taiyou no Mahou~");
    }

    [Test]
    public async Task Test_POST_vn_GetTitleBySearchingId()
    {
        var res = await Juliet.Api.POST_vn(new ParamPOST_vn()
        {
            Fields = new List<FieldPOST_vn>() { FieldPOST_vn.Id, FieldPOST_vn.Title, FieldPOST_vn.AltTitle },
            Filters = new Predicate(FilterField.Id, FilterOperator.Equal, "v7")
        });
        Console.WriteLine(JsonSerializer.Serialize(res,
            new JsonSerializerOptions() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping }));

        Assert.That(res!.Single().Results.Single().Title == "Tsukihime");
    }

    [Test]
    public async Task Test_POST_release_RawFilters()
    {
        var res = await Juliet.Api.POST_release(new ParamPOST_release()
        {
            Fields = new List<FieldPOST_release>()
            {
                FieldPOST_release.Id,
                FieldPOST_release.Title,
                FieldPOST_release.AltTitle,
                FieldPOST_release.Released,
                FieldPOST_release.ProducersDeveloper,
                FieldPOST_release.ProducersPublisher,
                FieldPOST_release.ProducersName,
                FieldPOST_release.ProducersOriginal,
            },
            RawFilters = "N482gla",
        });
        Console.WriteLine(JsonSerializer.Serialize(res,
            new JsonSerializerOptions() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping }));

        Assert.That(res!.Single().Results.First().Producers.First(x => x.Publisher ?? false).Name == "Ezo2");
    }

    [Test]
    public async Task Test_POST_release_WithPredicateAsValue()
    {
        var res = await Juliet.Api.POST_release(new ParamPOST_release()
        {
            Fields = new List<FieldPOST_release>()
            {
                FieldPOST_release.Id,
                FieldPOST_release.Title,
                FieldPOST_release.AltTitle,
                FieldPOST_release.Released,
                FieldPOST_release.ProducersDeveloper,
                FieldPOST_release.ProducersPublisher,
                FieldPOST_release.ProducersName,
                FieldPOST_release.ProducersOriginal,
            },
            Filters = new Predicate(FilterField.Vn, FilterOperator.Equal,
                new Predicate(FilterField.Id, FilterOperator.Equal, "v7")),
        });
        Console.WriteLine(JsonSerializer.Serialize(res,
            new JsonSerializerOptions() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping }));

        Assert.That(res!.Single().Results.First().Producers.First(x => x.Developer ?? false).Name == "TYPE-MOON");
    }
}
