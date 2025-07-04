namespace EMQ.Benchmarks;

internal readonly record struct AutocompleteMst2(ReadOnlyMemory<char> MSTLatinTitleNormalized,
    ReadOnlyMemory<char> MSTNonLatinTitleNormalized)
{
    public readonly ReadOnlyMemory<char> MSTLatinTitleNormalized = MSTLatinTitleNormalized;

    public readonly ReadOnlyMemory<char> MSTNonLatinTitleNormalized = MSTNonLatinTitleNormalized;
}
