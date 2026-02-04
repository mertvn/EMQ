using System;
using System.Buffers.Binary;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Text;

namespace EMQ.Client;

[SuppressMessage("ReSharper", "InconsistentNaming")]
public struct AutocompleteCharacter
{
    public string AALatinAlias;
    public string? AALatinAliasNormalized;
    public string? AANonLatinAliasNormalized;
    public string? AALatinAliasNormalizedReversed;
    public string? AANonLatinAliasNormalizedReversed;
}

public static class AutocompleteCharacterSerializer
{
    private static readonly Encoding s_utf8 = Encoding.UTF8;

    public static byte[] SerializeArray(AutocompleteCharacter[] items)
    {
        // 1. First Pass: Calculate total memory required for the entire array
        int totalSize = 4; // 4 bytes for the array length header

        // We store lengths temporarily to avoid recalculating UTF8 byte counts twice
        // For very large arrays, consider using a struct or rent from ArrayPool
        int[][] itemStringLengths = new int[items.Length][];

        for (int i = 0; i < items.Length; i++)
        {
            itemStringLengths[i] = new int[5];
            totalSize += 1; // 1 byte for bitmask per item

            itemStringLengths[i][0] = s_utf8.GetByteCount(items[i].AALatinAlias);
            totalSize += 4 + itemStringLengths[i][0];

            if (items[i].AALatinAliasNormalized != null)
            {
                itemStringLengths[i][1] = s_utf8.GetByteCount(items[i].AALatinAliasNormalized!);
                totalSize += 4 + itemStringLengths[i][1];
            }

            if (items[i].AANonLatinAliasNormalized != null)
            {
                itemStringLengths[i][2] = s_utf8.GetByteCount(items[i].AANonLatinAliasNormalized!);
                totalSize += 4 + itemStringLengths[i][2];
            }

            if (items[i].AALatinAliasNormalizedReversed != null)
            {
                itemStringLengths[i][3] = s_utf8.GetByteCount(items[i].AALatinAliasNormalizedReversed!);
                totalSize += 4 + itemStringLengths[i][3];
            }

            if (items[i].AANonLatinAliasNormalizedReversed != null)
            {
                itemStringLengths[i][4] = s_utf8.GetByteCount(items[i].AANonLatinAliasNormalizedReversed!);
                totalSize += 4 + itemStringLengths[i][4];
            }
        }

        // 2. Allocate the single buffer
        byte[] buffer = new byte[totalSize];
        Span<byte> span = buffer;

        // 3. Second Pass: Write the data
        BinaryPrimitives.WriteInt32LittleEndian(span[..4], items.Length);
        int offset = 4;

        for (int i = 0; i < items.Length; i++)
        {
            var item = items[i];
            int[] lengths = itemStringLengths[i];

            // Build bitmask
            byte bitmask = 0;
            if (item.AALatinAliasNormalized != null) bitmask |= 1 << 1;
            if (item.AANonLatinAliasNormalized != null) bitmask |= 1 << 2;
            if (item.AALatinAliasNormalizedReversed != null) bitmask |= 1 << 3;
            if (item.AANonLatinAliasNormalizedReversed != null) bitmask |= 1 << 4;

            span[offset++] = bitmask;

            // Write fields
            WriteString(span, item.AALatinAlias, lengths[0], ref offset);
            if (item.AALatinAliasNormalized != null)
                WriteString(span, item.AALatinAliasNormalized, lengths[1], ref offset);
            if (item.AANonLatinAliasNormalized != null)
                WriteString(span, item.AANonLatinAliasNormalized, lengths[2], ref offset);
            if (item.AALatinAliasNormalizedReversed != null)
                WriteString(span, item.AALatinAliasNormalizedReversed, lengths[3], ref offset);
            if (item.AANonLatinAliasNormalizedReversed != null)
                WriteString(span, item.AANonLatinAliasNormalizedReversed, lengths[4], ref offset);
        }

        return buffer;
    }

    public static AutocompleteCharacter[] DeserializeArray(ReadOnlySpan<byte> span)
    {
        // 1. Read array count
        int count = BinaryPrimitives.ReadInt32LittleEndian(span[..4]);
        int offset = 4;

        AutocompleteCharacter[] result = new AutocompleteCharacter[count];

        // 2. Loop through and parse each struct
        for (int i = 0; i < count; i++)
        {
            byte bitmask = span[offset++];
            AutocompleteCharacter item = new() { AALatinAlias = ReadString(span, ref offset) };
            if ((bitmask & (1 << 1)) != 0) item.AALatinAliasNormalized = ReadString(span, ref offset);
            if ((bitmask & (1 << 2)) != 0) item.AANonLatinAliasNormalized = ReadString(span, ref offset);
            if ((bitmask & (1 << 3)) != 0) item.AALatinAliasNormalizedReversed = ReadString(span, ref offset);
            if ((bitmask & (1 << 4)) != 0) item.AANonLatinAliasNormalizedReversed = ReadString(span, ref offset);

            result[i] = item;
        }

        return result;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void WriteString(Span<byte> span, string value, int length, ref int offset)
    {
        BinaryPrimitives.WriteInt32LittleEndian(span.Slice(offset, 4), length);
        offset += 4;
        s_utf8.GetBytes(value, span.Slice(offset, length));
        offset += length;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static string ReadString(ReadOnlySpan<byte> span, ref int offset)
    {
        int length = BinaryPrimitives.ReadInt32LittleEndian(span.Slice(offset, 4));
        offset += 4;
        string value = s_utf8.GetString(span.Slice(offset, length));
        offset += length;
        return value;
    }
}
