using System;
using System.Security.Cryptography;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;
using EMQ.Server;
using EMQ.Server.Db;
using EMQ.Shared.Quiz.Entities.Concrete;

namespace EMQ.Benchmarks;

[MemoryDiagnoser]
public class Md5VsSha256
{
    private int[] mids;

    public Md5VsSha256()
    {
        Console.WriteLine(Directory.GetCurrentDirectory());
        DotEnv.Load(@"todo");
        DbManager.Init();
        mids = (DbManager.FindRQs(DateTime.UtcNow.AddDays(-17), DateTime.UtcNow.AddDays(1))).GetAwaiter().GetResult()
            .Select(x => x.music_id).ToArray();
    }

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
}

public class Program
{
    public static void Main(string[] args)
    {
        var summary = BenchmarkRunner.Run<Md5VsSha256>();
    }
}
