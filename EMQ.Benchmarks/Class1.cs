using System;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;
using EMQ.Server;
using EMQ.Server.Db;
using EMQ.Shared.Core;
using EMQ.Shared.Library.Entities.Concrete;
using EMQ.Shared.Quiz.Entities.Concrete;

namespace EMQ.Benchmarks;

[MemoryDiagnoser]
// [ReturnValueValidator(failOnError: true)]
public class Md5VsSha256
{
    // private int[] mids;

    public readonly AutocompleteMst[] AutocompleteData;

    public Md5VsSha256()
    {
        // Console.WriteLine(Directory.GetCurrentDirectory());
        // DotEnv.Load(@"todo");
        // DbManager.Init().GetAwaiter().GetResult();
        // mids = (DbManager.FindRQs(DateTime.UtcNow.AddDays(-17), DateTime.UtcNow.AddDays(1))).GetAwaiter().GetResult()
        //     .Select(x => x.music_id).ToArray();

        AutocompleteData =
            JsonSerializer.Deserialize<AutocompleteMst[]>(DbManager
                .SelectAutocompleteMst(new[] { SongSourceType.VN, SongSourceType.Other }).GetAwaiter().GetResult())!;
    }

    // [Benchmark]
    // public void If()
    // {
    //     var input = "hashi";
    //     var str = "hashimoto miyuki";
    //     _ = str.StartsWithContains(input, StringComparison.Ordinal);
    // }
    //
    // [Benchmark]
    // public void Switch()
    // {
    //     var input = "hashi";
    //     var str = "hashimoto miyuki";
    //     _ = str.StartsWithContainsSw(input, StringComparison.Ordinal);
    // }

    // [Benchmark]
    // public async Task<List<Song>> LinqDelegate() => await DbManager.SelectSongsMIdsCachedLinqDelegate(mids, false);
    //
    // [Benchmark]
    // public async Task<List<Song>> LinqLambda() => await DbManager.SelectSongsMIdsCachedLinqLambda(mids, false);
    //
    // [Benchmark]
    // public async Task<List<Song>> TryGetValueYield() => await DbManager.SelectSongsMIdsCached(mids, false);
    //
    // [Benchmark]
    // public async Task<List<Song>> TryGetValueInline() =>
    //     await DbManager.SelectSongsMIdsCachedTryGetValueInline(mids, false);
    //
    // [Benchmark]
    // public async Task<List<Song>> TryGetValueInlineTuple() =>
    //     await DbManager.SelectSongsMIdsCachedTryGetValueInlineTuple(mids, false);
    //
    // [Benchmark]
    // public async Task<List<Song>> TryGetValueInlineDictionary() =>
    //     await DbManager.SelectSongsMIdsCachedTryGetValueInlineDictionary(mids, false);
    //
    // [Benchmark]
    // public async Task<List<Song>> TryGetValueInlineSeparateCollectionForKeys() =>
    //     await DbManager.SelectSongsMIdsCachedTryGetValueInlineSeparateCollectionForKeys(mids, false);

    // public TValue[] OnSearchOld<TValue>(string value)
    // {
    //     value = value.NormalizeForAutocomplete();
    //     if (string.IsNullOrWhiteSpace(value))
    //     {
    //         return Array.Empty<TValue>();
    //     }
    //
    //     bool hasNonAscii = !Ascii.IsValid(value);
    //     const int maxResults = 25; // todo
    //     var dictLT = new Dictionary<AutocompleteMst, StringMatch>();
    //     var dictNLT = new Dictionary<AutocompleteMst, StringMatch>();
    //     var valueSpan = value.AsSpan();
    //     foreach (AutocompleteMst d in AutocompleteData)
    //     {
    //         var matchLT = d.MSTLatinTitleNormalized.AsSpan()
    //             .StartsWithContains(valueSpan, StringComparison.Ordinal);
    //         if (matchLT > 0)
    //         {
    //             dictLT[d] = matchLT;
    //         }
    //
    //         if (hasNonAscii)
    //         {
    //             var matchNLT = d.MSTNonLatinTitleNormalized.AsSpan()
    //                 .StartsWithContains(valueSpan, StringComparison.Ordinal);
    //             if (matchNLT > 0)
    //             {
    //                 dictNLT[d] = matchNLT;
    //             }
    //         }
    //     }
    //
    //     return (TValue[])(object)dictLT.Concat(dictNLT)
    //         .OrderByDescending(x => x.Value)
    //         .DistinctBy(x => x.Key.MSTLatinTitle)
    //         .Take(maxResults)
    //         .Select(x => x.Key)
    //         .ToArray();
    // }
    //
    // private TValue[] OnSearchNew<TValue>(string value)
    // {
    //     if (string.IsNullOrWhiteSpace(value))
    //     {
    //         return Array.Empty<TValue>();
    //     }
    //
    //     value = value.NormalizeForAutocomplete();
    //     bool hasNonAscii = !Ascii.IsValid(value);
    //     const int maxResults = 25;
    //
    //     // Optimization 3: Pre-allocate with expected capacity
    //     var results = new HashSet<(AutocompleteMst Key, StringMatch Value)>(maxResults * 2);
    //
    //     var valueSpan = value.AsSpan();
    //
    //     // Optimization 4: Single pass through data
    //     foreach (AutocompleteMst d in AutocompleteData)
    //     {
    //         var matchLT = d.MSTLatinTitleNormalized.AsSpan()
    //             .StartsWithContains(valueSpan, StringComparison.Ordinal);
    //
    //         if (matchLT > 0)
    //         {
    //             results.Add((d, matchLT));
    //         }
    //
    //         if (hasNonAscii)
    //         {
    //             var matchNLT = d.MSTNonLatinTitleNormalized.AsSpan()
    //                 .StartsWithContains(valueSpan, StringComparison.Ordinal);
    //
    //             if (matchNLT > 0)
    //             {
    //                 results.Add((d, matchNLT));
    //             }
    //         }
    //     }
    //
    //     // Optimization 5: Use array-based sorting for better performance
    //     return (TValue[])(object)results
    //         .OrderByDescending(x => x.Value)
    //         .DistinctBy(x => x.Key.MSTLatinTitle)
    //         .Take(maxResults)
    //         .Select(x => x.Key)
    //         .ToArray();
    // }
    //
    // [Benchmark]
    // public AutocompleteMst[] OnSearchOld_1() => OnSearchOld<AutocompleteMst>("a");
    //
    // [Benchmark]
    // public AutocompleteMst[] OnSearchOld_3() => OnSearchOld<AutocompleteMst>("atl");
    //
    // [Benchmark]
    // public AutocompleteMst[] OnSearchNew_1() => OnSearchNew<AutocompleteMst>("a");
    //
    // [Benchmark]
    // public AutocompleteMst[] OnSearchNew_3() => OnSearchNew<AutocompleteMst>("atl");

    // [Benchmark]
    // public async Task<string?> GetPublicUserInfoSongs_Uncached() => await DbManager.GetPublicUserInfoSongs_Uncached(2);
    //
    // [Benchmark]
    // public async Task<string?> GetPublicUserInfoSongs_Cached() => await DbManager.GetPublicUserInfoSongsCached(2);
    //
    // [Benchmark]
    // public async Task<string?> GetPublicUserInfoSongs_CachedCloned() => await DbManager.GetPublicUserInfoSongs(2);
}

public class Program
{
    public static void Main(string[] args)
    {
        _ = BenchmarkRunner.Run<StartsWithContainsBenchmarks>();
    }
}
