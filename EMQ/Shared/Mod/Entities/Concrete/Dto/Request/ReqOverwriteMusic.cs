using System.ComponentModel.DataAnnotations;
using EMQ.Shared.Quiz.Entities.Concrete;

namespace EMQ.Shared.Mod.Entities.Concrete.Dto.Request;

public class ReqOverwriteMusic
{
    public ReqOverwriteMusic(int oldMid, Song newSong)
    {
        OldMid = oldMid;
        NewSong = newSong;
    }

    public int OldMid { get; set; }

    public Song NewSong { get; set; }
}
