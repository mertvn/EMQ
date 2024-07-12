using System.ComponentModel.DataAnnotations;

namespace EMQ.Shared.Auth.Entities.Concrete.Dto.Request;

public class ReqUpsertMusicVote
{
    public ReqUpsertMusicVote(int musicId, short? vote)
    {
        MusicId = musicId;
        Vote = vote;
    }

    [Required]
    public int MusicId { get; }

    [Range(10, 100)]
    public short? Vote { get; }
}
