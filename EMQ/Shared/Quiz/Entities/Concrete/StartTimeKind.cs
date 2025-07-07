using System.ComponentModel;

namespace EMQ.Shared.Quiz.Entities.Concrete;

public enum StartTimeKind
{
    Random = 0,
    Easy = 5,
    Hard = 10,

    // NoInstrumentals = 50,
    // Instrumentals = 100,
    [Description("No vocals")]
    NoVocals = 150,
    Vocals = 200,
}
