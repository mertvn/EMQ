using EMQ.Shared.Core;

namespace EMQ.Shared.Quiz.Entities.Concrete;

public readonly record struct PlayerStats
{
    public string Username { get; init; }

    public int QuizCount { get; init; }

    public long TimesCorrect { get; init; }

    public long TimesPlayed { get; init; }

    public long TimesGuessed { get; init; }

    public long TotalGuessMs { get; init; }

    public float AvgDiff { get; init; }

    public int Erigs { get; init; }

    public float AvgOf8 { get; init; }

    public int OpHit { get; init; }

    public int EdHit { get; init; }

    public int InsHit { get; init; }

    public int BgmHit { get; init; }

    public int OpCount { get; init; }

    public int EdCount { get; init; }

    public int InsCount { get; init; }

    public int BgmCount { get; init; }

    public int RigsHit { get; init; }

    public int Rigs { get; init; }

    public int RigOp { get; init; }

    public int RigEd { get; init; }

    public int RigIns { get; init; }

    public int RigBgm { get; init; }

    public int UniqueSongs { get; init; }

    public int OfflistCount { get; init; }

    public int OfflistHit { get; init; }


    public float CorrectPercentage => ((float)TimesCorrect).Div0(TimesPlayed) * 100;

    public int AverageGuessMs => (int)((float)TotalGuessMs).Div0(TimesGuessed);

    public float RigGr => ((float)RigsHit).Div0(Rigs) * 100;

    public float RigRate => ((float)Rigs).Div0(TimesPlayed) * 100;

    public float OpGr => ((float)OpHit).Div0(OpCount) * 100;

    public float EdGr => ((float)EdHit).Div0(EdCount) * 100;

    public float InsGr => ((float)InsHit).Div0(InsCount) * 100;

    public float BgmGr => ((float)BgmHit).Div0(BgmCount) * 100;

    public float RigOpRate => ((float)RigOp).Div0(Rigs) * 100;

    public float RigEdRate => ((float)RigEd).Div0(Rigs) * 100;

    public float RigInsRate => ((float)RigIns).Div0(Rigs) * 100;

    public float RigBgmRate => ((float)RigBgm).Div0(Rigs) * 100;

    public float UniqueSongsRate => ((float)UniqueSongs).Div0(TimesPlayed) * 100;

    public float OfflistGr => ((float)OfflistHit).Div0(OfflistCount) * 100;
}
