using System.Text;
using System.Text.Json;
using BenchmarkDotNet.Attributes;
using EMQ.Server;
using EMQ.Server.Db;
using EMQ.Shared.Core;
using EMQ.Shared.Library.Entities.Concrete;
using EMQ.Shared.Quiz.Entities.Concrete;
using Utils = EMQ.Shared.Core.Utils;

namespace EMQ.Benchmarks;

[MemoryDiagnoser]
[ReturnValueValidator(failOnError: true)]
public class StartsWithContainsBenchmarks
{
    public StartsWithContainsBenchmarks()
    {
        Console.WriteLine(Directory.GetCurrentDirectory());
        DotEnv.Load(@"todo");
        DbManager.Init().GetAwaiter().GetResult();

        var stopwatch = new Utils.MyStopwatch();
        stopwatch.Start();

        stopwatch.StartSection("SelectAutocompleteMst");
        string data = DbManager.SelectAutocompleteMst(new[] { SongSourceType.VN, SongSourceType.Other })
            .GetAwaiter().GetResult();

        stopwatch.StartSection("Deserialize");
        _autocompleteData = JsonSerializer.Deserialize<AutocompleteMst[]>(data)!;

        stopwatch.StartSection("Select");
        _autocompleteData2 = _autocompleteData.Select(x =>
                new AutocompleteMst2(x.MSTLatinTitleNormalized.AsMemory(), x.MSTNonLatinTitleNormalized.AsMemory()))
            .ToArray();

        stopwatch.Stop();
    }

    private readonly AutocompleteMst[] _autocompleteData;

    private readonly AutocompleteMst2[] _autocompleteData2;

    // ReSharper disable once InconsistentNaming
    private string value { get; set; } = "one";

    private const StringComparison StringComparison = System.StringComparison.Ordinal;

    [Benchmark(Baseline = true)]
    public int Baseline()
    {
        var valueSpan = value.NormalizeForAutocomplete().AsSpan();
        bool hasNonAscii = !Ascii.IsValid(valueSpan);
        var dictLT = new Dictionary<AutocompleteMst, StringMatch>();
        var dictNLT = new Dictionary<AutocompleteMst, StringMatch>();

        foreach (AutocompleteMst d in _autocompleteData)
        {
            var matchLT = d.MSTLatinTitleNormalized.AsSpan().Baseline(valueSpan, StringComparison);
            if (matchLT > 0)
            {
                dictLT[d] = matchLT;
            }

            if (hasNonAscii)
            {
                var matchNLT = d.MSTNonLatinTitleNormalized.AsSpan().Baseline(valueSpan, StringComparison);
                if (matchNLT > 0)
                {
                    dictNLT[d] = matchNLT;
                }
            }
        }

        return dictLT.Count + dictNLT.Count;
    }

    [Benchmark]
    public int Memory()
    {
        var valueSpan = value.NormalizeForAutocomplete().AsSpan();
        bool hasNonAscii = !Ascii.IsValid(valueSpan);
        var dictLT = new Dictionary<AutocompleteMst2, StringMatch>();
        var dictNLT = new Dictionary<AutocompleteMst2, StringMatch>();

        foreach (AutocompleteMst2 d in _autocompleteData2)
        {
            var matchLT = d.MSTLatinTitleNormalized.Span.Baseline(valueSpan, StringComparison);
            if (matchLT > 0)
            {
                dictLT[d] = matchLT;
            }

            if (hasNonAscii)
            {
                var matchNLT = d.MSTNonLatinTitleNormalized.Span.Baseline(valueSpan, StringComparison);
                if (matchNLT > 0)
                {
                    dictNLT[d] = matchNLT;
                }
            }
        }

        return dictLT.Count + dictNLT.Count;
    }

    // [Benchmark]
    public int BaselineNoLength()
    {
        var valueSpan = value.NormalizeForAutocomplete().AsSpan();
        bool hasNonAscii = !Ascii.IsValid(valueSpan);
        var dictLT = new Dictionary<AutocompleteMst, StringMatch>();
        var dictNLT = new Dictionary<AutocompleteMst, StringMatch>();

        foreach (AutocompleteMst d in _autocompleteData)
        {
            var matchLT = d.MSTLatinTitleNormalized.AsSpan().BaselineNoLength(valueSpan, StringComparison);
            if (matchLT > 0)
            {
                dictLT[d] = matchLT;
            }

            if (hasNonAscii)
            {
                var matchNLT = d.MSTNonLatinTitleNormalized.AsSpan().BaselineNoLength(valueSpan, StringComparison);
                if (matchNLT > 0)
                {
                    dictNLT[d] = matchNLT;
                }
            }
        }

        return dictLT.Count + dictNLT.Count;
    }

    // [Benchmark]
    public int rampaa()
    {
        var valueSpan = value.NormalizeForAutocomplete().AsSpan();
        bool hasNonAscii = !Ascii.IsValid(valueSpan);
        var dictLT = new Dictionary<AutocompleteMst, StringMatch>();
        var dictNLT = new Dictionary<AutocompleteMst, StringMatch>();

        foreach (AutocompleteMst d in _autocompleteData)
        {
            var matchLT = d.MSTLatinTitleNormalized.AsSpan().rampaa(valueSpan, StringComparison);
            if (matchLT > 0)
            {
                dictLT[d] = matchLT;
            }

            if (hasNonAscii)
            {
                var matchNLT = d.MSTNonLatinTitleNormalized.AsSpan().rampaa(valueSpan, StringComparison);
                if (matchNLT > 0)
                {
                    dictNLT[d] = matchNLT;
                }
            }
        }

        return dictLT.Count + dictNLT.Count;
    }

    // [Benchmark]
    public int rampaa2()
    {
        var valueSpan = value.NormalizeForAutocomplete().AsSpan();
        bool hasNonAscii = !Ascii.IsValid(valueSpan);
        var dictLT = new Dictionary<AutocompleteMst, StringMatch>();
        var dictNLT = new Dictionary<AutocompleteMst, StringMatch>();

        foreach (AutocompleteMst d in _autocompleteData)
        {
            var matchLT = d.MSTLatinTitleNormalized.AsSpan().rampaa2(valueSpan);
            if (matchLT > 0)
            {
                dictLT[d] = matchLT;
            }

            if (hasNonAscii)
            {
                var matchNLT = d.MSTNonLatinTitleNormalized.AsSpan().rampaa2(valueSpan);
                if (matchNLT > 0)
                {
                    dictNLT[d] = matchNLT;
                }
            }
        }

        return dictLT.Count + dictNLT.Count;
    }

    // [Benchmark]
    public int Claude4Sonnet()
    {
        var valueSpan = value.NormalizeForAutocomplete().AsSpan();
        bool hasNonAscii = !Ascii.IsValid(valueSpan);
        var dictLT = new Dictionary<AutocompleteMst, StringMatch>();
        var dictNLT = new Dictionary<AutocompleteMst, StringMatch>();

        foreach (AutocompleteMst d in _autocompleteData)
        {
            var matchLT = d.MSTLatinTitleNormalized.AsSpan().Claude4Sonnet(valueSpan, StringComparison);
            if (matchLT > 0)
            {
                dictLT[d] = matchLT;
            }

            if (hasNonAscii)
            {
                var matchNLT = d.MSTNonLatinTitleNormalized.AsSpan().Claude4Sonnet(valueSpan, StringComparison);
                if (matchNLT > 0)
                {
                    dictNLT[d] = matchNLT;
                }
            }
        }

        return dictLT.Count + dictNLT.Count;
    }

    // [Benchmark]
    public int DeepSeekV3()
    {
        var valueSpan = value.NormalizeForAutocomplete().AsSpan();
        bool hasNonAscii = !Ascii.IsValid(valueSpan);
        var dictLT = new Dictionary<AutocompleteMst, StringMatch>();
        var dictNLT = new Dictionary<AutocompleteMst, StringMatch>();

        foreach (AutocompleteMst d in _autocompleteData)
        {
            var matchLT = d.MSTLatinTitleNormalized.AsSpan().DeepSeekV3(valueSpan, StringComparison);
            if (matchLT > 0)
            {
                dictLT[d] = matchLT;
            }

            if (hasNonAscii)
            {
                var matchNLT = d.MSTNonLatinTitleNormalized.AsSpan().DeepSeekV3(valueSpan, StringComparison);
                if (matchNLT > 0)
                {
                    dictNLT[d] = matchNLT;
                }
            }
        }

        return dictLT.Count + dictNLT.Count;
    }

    // [Benchmark]
    public int Gemini25Pro()
    {
        var valueSpan = value.NormalizeForAutocomplete().AsSpan();
        bool hasNonAscii = !Ascii.IsValid(valueSpan);
        var dictLT = new Dictionary<AutocompleteMst, StringMatch>();
        var dictNLT = new Dictionary<AutocompleteMst, StringMatch>();

        foreach (AutocompleteMst d in _autocompleteData)
        {
            var matchLT = d.MSTLatinTitleNormalized.AsSpan().Gemini25Pro(valueSpan, StringComparison);
            if (matchLT > 0)
            {
                dictLT[d] = matchLT;
            }

            if (hasNonAscii)
            {
                var matchNLT = d.MSTNonLatinTitleNormalized.AsSpan().Gemini25Pro(valueSpan, StringComparison);
                if (matchNLT > 0)
                {
                    dictNLT[d] = matchNLT;
                }
            }
        }

        return dictLT.Count + dictNLT.Count;
    }

    // [Benchmark]
    // ReSharper disable once InconsistentNaming
    public int GPT4o()
    {
        var valueSpan = value.NormalizeForAutocomplete().AsSpan();
        bool hasNonAscii = !Ascii.IsValid(valueSpan);
        var dictLT = new Dictionary<AutocompleteMst, StringMatch>();
        var dictNLT = new Dictionary<AutocompleteMst, StringMatch>();

        foreach (AutocompleteMst d in _autocompleteData)
        {
            var matchLT = d.MSTLatinTitleNormalized.AsSpan().GPT4o(valueSpan, StringComparison);
            if (matchLT > 0)
            {
                dictLT[d] = matchLT;
            }

            if (hasNonAscii)
            {
                var matchNLT = d.MSTNonLatinTitleNormalized.AsSpan().GPT4o(valueSpan, StringComparison);
                if (matchNLT > 0)
                {
                    dictNLT[d] = matchNLT;
                }
            }
        }

        return dictLT.Count + dictNLT.Count;
    }

    // [Benchmark]
    // ReSharper disable once InconsistentNaming
    public int GPTo4mini()
    {
        var valueSpan = value.NormalizeForAutocomplete().AsSpan();
        bool hasNonAscii = !Ascii.IsValid(valueSpan);
        var dictLT = new Dictionary<AutocompleteMst, StringMatch>();
        var dictNLT = new Dictionary<AutocompleteMst, StringMatch>();

        foreach (AutocompleteMst d in _autocompleteData)
        {
            var matchLT = d.MSTLatinTitleNormalized.AsSpan().GPTo4mini(valueSpan, StringComparison);
            if (matchLT > 0)
            {
                dictLT[d] = matchLT;
            }

            if (hasNonAscii)
            {
                var matchNLT = d.MSTNonLatinTitleNormalized.AsSpan().GPTo4mini(valueSpan, StringComparison);
                if (matchNLT > 0)
                {
                    dictNLT[d] = matchNLT;
                }
            }
        }

        return dictLT.Count + dictNLT.Count;
    }
}
