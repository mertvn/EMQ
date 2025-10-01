using System.Buffers;
using System.Globalization;
using System.Text;
using System.Text.Json;
using BenchmarkDotNet.Attributes;
using EMQ.Shared.Core;
using EMQ.Shared.Library.Entities.Concrete;

namespace EMQ.Benchmarks;

[MemoryDiagnoser]
public class NormalizeBenchmarks
{
    public NormalizeBenchmarks()
    {
        Data = JsonSerializer.Deserialize<AutocompleteA[]>(
            File.ReadAllText("todo/autocomplete/a.json"))!;
    }

    public AutocompleteA[] Data { get; }

    [Benchmark(Baseline = true)]
    public void Baseline()
    {
        foreach (AutocompleteA autocompleteA in Data)
        {
            _ = Impl_Baseline(autocompleteA.AALatinAlias);
            _ = Impl_Baseline(autocompleteA.AANonLatinAlias);
        }
    }

    [Benchmark]
    public void New()
    {
        foreach (AutocompleteA autocompleteA in Data)
        {
            _ = Impl_New(autocompleteA.AALatinAlias);
            _ = Impl_New(autocompleteA.AANonLatinAlias);
        }
    }

    [Benchmark]
    public void NewFull()
    {
        foreach (AutocompleteA autocompleteA in Data)
        {
            _ = Impl_NewFull(autocompleteA.AALatinAlias);
            _ = Impl_NewFull(autocompleteA.AANonLatinAlias);
        }
    }

    public static string Impl_Baseline(string input)
    {
        foreach ((string? key, string? value) in RegexPatterns.AutocompleteStringReplacements)
        {
            input = input.Replace(key, value);
        }

        return new string(input
                .Trim()
                .ToLowerInvariant()
                .Normalize(NormalizationForm.FormD)
                .Where(ch =>
                    CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark &&
                    char.IsLetterOrDigit(ch))
                .ToArray())
            .Normalize(NormalizationForm.FormC);
    }

    public static string Impl_New(string input)
    {
        if (string.IsNullOrEmpty(input))
            return string.Empty;

        // Apply replacements (same as old)
        foreach ((string? key, string? value) in RegexPatterns.AutocompleteStringReplacements)
        {
            input = input.Replace(key, value);
        }

        // Trim
        input = input.Trim();
        if (input.Length == 0)
            return string.Empty;

        // Allocate a pooled buffer large enough to hold the result
        var buffer = ArrayPool<char>.Shared.Rent(input.Length * 2); // allow expansion
        int outPos = 0;

        foreach (char c in input)
        {
            AutocompleteHelpers.NormalizeCharFast(c, buffer, ref outPos);
        }

        // Create normalized string and apply FormC
        string normalized = new string(buffer, 0, outPos).Normalize(NormalizationForm.FormC);

        ArrayPool<char>.Shared.Return(buffer);

        return normalized;
    }

    public static string Impl_NewFull(string input)
    {
        return AutocompleteHelpers.NormalizeForAutocomplete(input);
    }
}
