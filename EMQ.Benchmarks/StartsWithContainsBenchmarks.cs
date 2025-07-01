using System.Text;
using System.Text.Json;
using BenchmarkDotNet.Attributes;
using EMQ.Server;
using EMQ.Server.Db;
using EMQ.Shared.Core;
using EMQ.Shared.Library.Entities.Concrete;
using EMQ.Shared.Quiz.Entities.Concrete;

namespace EMQ.Benchmarks;

[MemoryDiagnoser]
public class StartsWithContainsBenchmarks
{
    public StartsWithContainsBenchmarks()
    {
        Console.WriteLine(Directory.GetCurrentDirectory());
        DotEnv.Load(@"../../../../../../../../.env");
        DbManager.Init().GetAwaiter().GetResult();

        _autocompleteData =
            JsonSerializer.Deserialize<AutocompleteMst[]>(DbManager
                .SelectAutocompleteMst(new[] { SongSourceType.VN, SongSourceType.Other }).GetAwaiter().GetResult())!;
    }

    private readonly AutocompleteMst[] _autocompleteData;

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

    [Benchmark]
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

    [Benchmark]
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

    [Benchmark]
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

    [Benchmark]
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
