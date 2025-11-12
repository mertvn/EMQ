using EMQ.Shared.Quiz.Entities.Concrete;

namespace EMQ.Shared.Library.Entities.Concrete.Dto.Request;

public class ReqGetLabelStats
{
    public ReqGetLabelStats(string presetName, SongSourceSongTypeMode ssstm)
    {
        PresetName = presetName;
        SSSTM = ssstm;
    }

    public string PresetName { get; }
    public SongSourceSongTypeMode SSSTM { get; }
}
