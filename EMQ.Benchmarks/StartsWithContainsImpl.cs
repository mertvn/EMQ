using System.Runtime.CompilerServices;
using EMQ.Shared.Core;

namespace EMQ.Benchmarks;

public static class StartsWithContainsImpl
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static StringMatch Baseline(this ReadOnlySpan<char> str, ReadOnlySpan<char> search,
        StringComparison stringComparison)
    {
        if (str.IsEmpty || search.IsEmpty)
        {
            return StringMatch.None;
        }

        int result = str.IndexOf(search, stringComparison);
        return result switch
        {
            < 0 => StringMatch.None,
            0 when str.Length != search.Length => StringMatch.StartsWith,
            0 when str.Equals(search, stringComparison) => StringMatch.ExactMatch,
            0 => StringMatch.StartsWith,
            _ => StringMatch.Contains
        };
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static StringMatch BaselineNoLength(this ReadOnlySpan<char> str, ReadOnlySpan<char> search,
        StringComparison stringComparison)
    {
        if (str.IsEmpty || search.IsEmpty)
        {
            return StringMatch.None;
        }

        int result = str.IndexOf(search, stringComparison);
        return result switch
        {
            < 0 => StringMatch.None,
            0 when str.Equals(search, stringComparison) => StringMatch.ExactMatch,
            0 => StringMatch.StartsWith,
            _ => StringMatch.Contains
        };
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static StringMatch rampaa(this ReadOnlySpan<char> text, ReadOnlySpan<char> searchText,
        StringComparison stringComparison)
    {
        if (text.IsEmpty || searchText.IsEmpty)
        {
            return StringMatch.None;
        }

        int index = text.IndexOf(searchText, stringComparison);
        return index < 0
            ? StringMatch.None
            : index is 0
                ? text.Length != searchText.Length
                    ? StringMatch.StartsWith
                    : StringMatch.ExactMatch
                : StringMatch.Contains;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static StringMatch rampaa2(this ReadOnlySpan<char> text, ReadOnlySpan<char> searchText)
    {
        if (searchText.IsEmpty)
        {
            return StringMatch.None;
        }

        int index = text.IndexOf(searchText);
        return index < 0
            ? StringMatch.None
            : index is 0
                ? text.Length != searchText.Length
                    ? StringMatch.StartsWith
                    : StringMatch.ExactMatch
                : StringMatch.Contains;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static StringMatch Claude4Sonnet(this ReadOnlySpan<char> str, ReadOnlySpan<char> search,
        StringComparison stringComparison)
    {
        if (str.IsEmpty | search.IsEmpty)
            return StringMatch.None;

        // Fast path for exact match
        if (str.Length == search.Length)
        {
            return stringComparison == StringComparison.Ordinal
                ? str.SequenceEqual(search) ? StringMatch.ExactMatch : StringMatch.None
                : str.Equals(search, stringComparison)
                    ? StringMatch.ExactMatch
                    : StringMatch.None;
        }

        // Fast path for impossible cases
        if (str.Length < search.Length)
            return StringMatch.None;

        // Optimized starts-with check
        if (stringComparison == StringComparison.Ordinal)
        {
            if (str.Slice(0, search.Length).SequenceEqual(search))
                return StringMatch.StartsWith;
        }
        else
        {
            if (str.StartsWith(search, stringComparison))
                return StringMatch.StartsWith;
        }

        // Only do expensive IndexOf if we need to check contains
        int index = str.IndexOf(search, stringComparison);
        return index >= 0 ? StringMatch.Contains : StringMatch.None;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static StringMatch DeepSeekV3(this ReadOnlySpan<char> str, ReadOnlySpan<char> search,
        StringComparison stringComparison)
    {
        // Fast path for empty inputs
        if (str.Length == 0 || search.Length == 0)
        {
            return StringMatch.None;
        }

        // Check for exact match first (common case optimization)
        if (str.Length == search.Length)
        {
            return str.Equals(search, stringComparison) ? StringMatch.ExactMatch : StringMatch.None;
        }

        // Check for starts with (another common case)
        if (str.StartsWith(search, stringComparison))
        {
            return StringMatch.StartsWith;
        }

        // Only perform full search if needed
        int result = str.IndexOf(search, stringComparison);
        return result >= 0 ? StringMatch.Contains : StringMatch.None;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static StringMatch Gemini25Pro(this ReadOnlySpan<char> str, ReadOnlySpan<char> search,
        StringComparison stringComparison)
    {
        // 1. Handle empty inputs first (fastest check)
        if (str.IsEmpty || search.IsEmpty || search.Length > str.Length)
        {
            return StringMatch.None;
        }

        // 2. Check for the "StartsWith" case directly. This is often faster
        //    than a full IndexOf scan, as it only needs to check the beginning.
        if (str.StartsWith(search, stringComparison))
        {
            // If it starts with the search string, the only other possibility
            // is an exact match, which we can determine with a cheap length check.
            // This completely avoids a second, redundant comparison like str.Equals().
            return str.Length == search.Length ? StringMatch.ExactMatch : StringMatch.StartsWith;
        }

        // 3. If it doesn't start with the search string, check if it's contained elsewhere.
        //    We can optimize by slicing off the first character, as we've already
        //    checked it. This is a micro-optimization but avoids redundant work.
        if (str.Slice(1).Contains(search, stringComparison))
        {
            return StringMatch.Contains;
        }

        // 4. If none of the above, there is no match.
        return StringMatch.None;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static StringMatch GPT4o(this ReadOnlySpan<char> str, ReadOnlySpan<char> search, StringComparison comparison)
    {
        if (str.IsEmpty || search.IsEmpty || search.Length > str.Length)
            return StringMatch.None;

        int index = str.IndexOf(search, comparison);
        if (index < 0)
            return StringMatch.None;

        if (index == 0)
        {
            if (str.Length == search.Length)
                return StringMatch.ExactMatch;

            return StringMatch.StartsWith;
        }

        return StringMatch.Contains;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static StringMatch GPTo4mini(this ReadOnlySpan<char> str,
        ReadOnlySpan<char> search,
        StringComparison comparison)
    {
        // Quick exits
        int strLen = str.Length;
        int searchLen = search.Length;
        if (searchLen == 0 || strLen < searchLen)
            return StringMatch.None;

        // Exact-length -> either ExactMatch or fall through to StartsWith logic
        if (strLen == searchLen)
        {
            return str.Equals(search, comparison)
                ? StringMatch.ExactMatch
                : StringMatch.None;
        }

        // Check prefix
        if (str.StartsWith(search, comparison))
        {
            return StringMatch.StartsWith;
        }

        // Check anywhere else
        return str.IndexOf(search, comparison) >= 0
            ? StringMatch.Contains
            : StringMatch.None;
    }
}
