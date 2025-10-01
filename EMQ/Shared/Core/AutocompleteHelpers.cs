using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace EMQ.Shared.Core;

public static class AutocompleteHelpers
{
    private readonly struct Token
    {
        public Token(int origStart, int origEnd, int repStart, int repEnd, string replaced,
            string normalizedFragment)
        {
            OrigStart = origStart;
            OrigEnd = origEnd;
            ReplacedStart = repStart;
            ReplacedEnd = repEnd;
            Replaced = replaced;
            NormalizedFragment = normalizedFragment;
        }

        public readonly int OrigStart;
        public readonly int OrigEnd;
        public readonly int ReplacedStart;
        public readonly int ReplacedEnd;
        public readonly string Replaced;
        public readonly string NormalizedFragment;

        public Token WithNormalized(string norm) => new(OrigStart, OrigEnd, ReplacedStart, ReplacedEnd, Replaced, norm);
    }

    static AutocompleteHelpers()
    {
        // Precompute and cache replacement keys sorted by length descending
        var dict = RegexPatterns.AutocompleteStringReplacements;
        s_replacementRules = dict.Keys
            .Select(k => (k, dict[k]))
            .OrderByDescending(x => x.Item1.Length)
            .ToArray();
    }

    private static readonly (string Key, string Replacement)[] s_replacementRules;

    private static readonly ConcurrentDictionary<char, string> s_unicodeCache = new();

    private static (string Normalized, Token[] Tokens) TokenizeAndNormalize(string input)
    {
        if (string.IsNullOrEmpty(input))
        {
            return (string.Empty, Array.Empty<Token>());
        }

        // rent buffer for replacements
        var charPool = ArrayPool<char>.Shared;
        char[] rentedBuffer = charPool.Rent(input.Length * 2); // allow expansion
        int outPos = 0;

        var tokenList = new List<Token>(input.Length);
        int i = 0;
        while (i < input.Length)
        {
            bool matched = false;
            foreach (var (key, replacement) in s_replacementRules)
            {
                if (i + key.Length <= input.Length &&
                    input.AsSpan(i, key.Length).SequenceEqual(key.AsSpan()))
                {
                    int repStart = outPos;
                    replacement.AsSpan().CopyTo(rentedBuffer.AsSpan(outPos));
                    outPos += replacement.Length;

                    tokenList.Add(new Token(i, i + key.Length - 1, repStart, outPos - 1, replacement,
                        string.Empty));

                    i += key.Length;
                    matched = true;
                    break;
                }
            }

            if (!matched)
            {
                int repStart = outPos;
                rentedBuffer[outPos++] = input[i];
                tokenList.Add(new Token(i, i, repStart, outPos - 1, input[i].ToString(), string.Empty));
                i++;
            }
        }

        var replacedSpan = rentedBuffer.AsSpan(0, outPos);

        // Trim
        int repTrimStart = 0;
        while (repTrimStart < replacedSpan.Length && char.IsWhiteSpace(replacedSpan[repTrimStart]))
        {
            repTrimStart++;
        }

        int repTrimEnd = replacedSpan.Length - 1;
        while (repTrimEnd >= repTrimStart && char.IsWhiteSpace(replacedSpan[repTrimEnd]))
        {
            repTrimEnd--;
        }

        // normalize fragments directly into pooled buffer
        var normPool = ArrayPool<char>.Shared;
        char[] normBuffer = normPool.Rent(replacedSpan.Length * 4);
        int normPos = 0;

        var tokensOut = new Token[tokenList.Count];
        for (int tIdx = 0; tIdx < tokenList.Count; tIdx++)
        {
            var t = tokenList[tIdx];
            if (t.ReplacedEnd < repTrimStart || t.ReplacedStart > repTrimEnd)
            {
                tokensOut[tIdx] = t.WithNormalized(string.Empty);
                continue;
            }

            int sliceStart = Math.Max(repTrimStart - t.ReplacedStart, 0);
            int sliceLen = Math.Min(t.Replaced.Length - sliceStart, repTrimEnd - t.ReplacedStart - sliceStart + 1);
            if (sliceLen <= 0)
            {
                tokensOut[tIdx] = t.WithNormalized(string.Empty);
                continue;
            }

            var slice = t.Replaced.AsSpan(sliceStart, sliceLen);
            int before = normPos;
            foreach (char ch in slice)
            {
                NormalizeCharFast(ch, normBuffer, ref normPos);
            }

            string normFrag = new(normBuffer, before, normPos - before);
            tokensOut[tIdx] = t.WithNormalized(normFrag);
        }

        string normalized = new(normBuffer, 0, normPos);
        charPool.Return(rentedBuffer);
        normPool.Return(normBuffer);
        return (normalized, tokensOut);
    }

    /// <summary>
    /// Writes the normalized representation of a character into the output buffer.
    /// ASCII fast-path: 'a'..'z' (unchanged), 'A'..'Z' (lowercased), '0'..'9' (unchanged).
    /// Non-ASCII: cached normalization result.
    /// </summary>
    public static void NormalizeCharFast(char c, char[] buffer, ref int outPos)
    {
        // ASCII lowercase
        if ((uint)(c - 'a') <= 25)
        {
            buffer[outPos++] = c;
            return;
        }

        // ASCII uppercase -> lowercase
        if ((uint)(c - 'A') <= 25)
        {
            buffer[outPos++] = (char)(c + 32);
            return;
        }

        // ASCII digit
        if ((uint)(c - '0') <= 9)
        {
            buffer[outPos++] = c;
            return;
        }

        if (char.IsSurrogate(c))
        {
            return;
        }

        // Non-ASCII: use cache
        if (!s_unicodeCache.TryGetValue(c, out string? cached))
        {
            cached = s_unicodeCache.GetOrAdd(c, static c => NormalizeSlow(c.ToString()));
        }

        cached.AsSpan().CopyTo(buffer.AsSpan(outPos));
        outPos += cached.Length;
    }

    /// <summary>
    /// Slow fallback normalization: lowercase + FormD + strip diacritics.
    /// </summary>
    private static string NormalizeSlow(string s)
    {
        if (string.IsNullOrEmpty(s))
        {
            return string.Empty;
        }

        string lower = s.ToLowerInvariant().Normalize(NormalizationForm.FormD);
        Span<char> tmp = stackalloc char[lower.Length];
        int count = 0;
        foreach (char ch in lower)
        {
            var cat = CharUnicodeInfo.GetUnicodeCategory(ch);
            if (cat != UnicodeCategory.NonSpacingMark && char.IsLetterOrDigit(ch))
            {
                tmp[count++] = ch;
            }
        }

        return new string(tmp[..count]);
    }

    // === Public APIs ===

    public static string NormalizeForAutocomplete(this string input)
    {
        (string norm, _) = TokenizeAndNormalize(input);
        return norm;
    }

    // todo? for maximum performance we could take selectedNorm as a parameter here, as we technically already have it from performing the search
    // well we could also have the textFieldNorm but that would require us to serialize the tokens etc.
    public static (int originalStart, int originalEnd) GetOriginalMatchSpan(string textField, string selectedText)
    {
        if (string.IsNullOrEmpty(textField) || string.IsNullOrEmpty(selectedText))
        {
            return (-1, -1);
        }

        string selectedNorm = selectedText.NormalizeForAutocomplete();
        (string textFieldNorm, Token[] tokens) = TokenizeAndNormalize(textField);

        if (string.IsNullOrEmpty(textFieldNorm) || string.IsNullOrEmpty(selectedNorm))
        {
            return (-1, -1);
        }

        int matchStart = textFieldNorm.IndexOf(selectedNorm, StringComparison.OrdinalIgnoreCase);
        if (matchStart < 0)
        {
            return (-1, -1);
        }

        int matchEnd = matchStart + selectedNorm.Length - 1;

        int cursor = 0;
        int tokenStartIndex = -1, tokenEndIndex = -1;
        for (int idx = 0; idx < tokens.Length; idx++)
        {
            string frag = tokens[idx].NormalizedFragment;
            int fragLen = frag.Length;
            if (fragLen == 0)
            {
                continue;
            }

            int tStart = cursor;
            int tEnd = cursor + fragLen - 1;

            if (tokenStartIndex == -1 && matchStart >= tStart && matchStart <= tEnd)
            {
                tokenStartIndex = idx;
            }

            if (matchEnd >= tStart && matchEnd <= tEnd)
            {
                tokenEndIndex = idx;
                break;
            }

            cursor += fragLen;
        }

        if (tokenStartIndex == -1 || tokenEndIndex == -1)
        {
            return (-1, -1);
        }

        return (tokens[tokenStartIndex].OrigStart, tokens[tokenEndIndex].OrigEnd);
    }
}
