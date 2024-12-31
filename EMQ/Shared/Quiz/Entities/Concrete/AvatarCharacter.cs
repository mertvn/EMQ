using System.ComponentModel;

namespace EMQ.Shared.Quiz.Entities.Concrete;

public enum AvatarCharacter
{
    Auu,

    [Description("VNDB")]
    VNDBCharacterImage,

    [Description("Procras & Tina")]
    ProcrasAndTina,
}
