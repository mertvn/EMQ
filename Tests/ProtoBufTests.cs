using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using EMQ.Shared.Core;
using EMQ.Shared.Library.Entities.Concrete;
using EMQ.Shared.Quiz.Entities.Concrete;
using NUnit.Framework;

namespace Tests;

public class ProtoBufTests
{
    [Test]
    public async Task Test_CanSerializeData()
    {
        const int numSongs = 78;
        const int guessMs = 7000;
        const int resultsMs = 8000;
        var filters = new QuizFilters
        {
            VNOLangs = new Dictionary<Language, bool> { { Language.ja, false }, { Language.en, true } },
            ArtistFilters =
                new List<ArtistFilter>
                {
                    new(new AutocompleteA(11111, "s22", "Shimotsuki Haruka", "霜月 はるか"), LabelKind.Include)
                },
            CategoryFilters = new List<CategoryFilter>
            {
                new(new SongSourceCategory { Type = SongSourceCategoryType.Tag }, LabelKind.Exclude)
            }
        };

        var q = new QuizSettings
        {
            NumSongs = numSongs, GuessMs = guessMs, ResultsMs = resultsMs, Filters = filters,
        };
        string ser = q.SerializeToBase64String_PB();
        Console.WriteLine(ser);
        Console.WriteLine(ser.Length);

        Assert.That(ser.Length > 200);
    }

    [Test]
    public async Task Test_CanDeserializeOldData()
    {
        const string serialized =
            "CE4Q2DYYwD6CAcgBChEKBBIAIAEQ////////////ARIxCi0I51YSA3MyMhoRU2hpbW90c3VraSBIYXJ1a2EiEOmcnOaciCDjga/jgovjgYsQARoAIgIIASIECAIQASoECAESACoECAISACoECAMSACoECAQSACoHCIkGEgIIKDIECAEQATIECAIQATIECAMQATIECAQQAToECAAQAToECAEQAToECAIQAToECAMQAToECAQQAToECAUQAUIDCNxmSgQIttYCUGRY6AdgZGjoB3iowwE=";

        const int numSongs = 78;
        const int guessMs = 7000;
        const int resultsMs = 8000;
        var filters = new QuizFilters
        {
            VNOLangs = new Dictionary<Language, bool> { { Language.ja, false }, { Language.en, true } },
            ArtistFilters =
                new List<ArtistFilter>
                {
                    new(new AutocompleteA(11111, "s22", "Shimotsuki Haruka", "霜月 はるか"), LabelKind.Include)
                },
            CategoryFilters = new List<CategoryFilter>
            {
                new(new SongSourceCategory { Type = SongSourceCategoryType.Tag }, LabelKind.Exclude)
            }
        };

        // var q = new QuizSettings
        // {
        //     NumSongs = numSongs, GuessMs = guessMs, ResultsMs = resultsMs, Filters = filters,
        // };
        // string ser = q.SerializeToBase64String_PB();
        // Console.WriteLine(ser);
        // Console.WriteLine(ser.Length);
        // return;

        var deserialized = serialized.DeserializeFromBase64String_PB<QuizSettings>();
        Console.WriteLine(JsonSerializer.Serialize(deserialized, Utils.Jso));

        Assert.That(deserialized.NumSongs == numSongs);
        Assert.That(deserialized.GuessMs == guessMs);
        Assert.That(deserialized.ResultsMs == resultsMs);

        Assert.That(!deserialized.Filters.VNOLangs[Language.ja]);
        Assert.That(deserialized.Filters.VNOLangs[Language.en]);

        Assert.That(deserialized.Filters.ArtistFilters.First().Artist.AId == filters.ArtistFilters.First().Artist.AId);
        Assert.That(deserialized.Filters.ArtistFilters.First().Artist.VndbId ==
                    filters.ArtistFilters.First().Artist.VndbId);
        Assert.That(deserialized.Filters.ArtistFilters.First().Artist.AALatinAlias ==
                    filters.ArtistFilters.First().Artist.AALatinAlias);
        Assert.That(deserialized.Filters.ArtistFilters.First().Artist.AANonLatinAlias ==
                    filters.ArtistFilters.First().Artist.AANonLatinAlias);
        Assert.That(deserialized.Filters.ArtistFilters.First().Trilean == filters.ArtistFilters.First().Trilean);

        Assert.That(deserialized.Filters.CategoryFilters.First().SongSourceCategory.Type ==
                    filters.CategoryFilters.First().SongSourceCategory.Type);
        Assert.That(deserialized.Filters.CategoryFilters.First().Trilean == filters.CategoryFilters.First().Trilean);
    }
}
