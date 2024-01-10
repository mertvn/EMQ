using System.ComponentModel.DataAnnotations;

namespace EMQ.Shared.Mod.Entities.Concrete.Dto.Request;

public class ReqDeleteSongLink
{
    public ReqDeleteSongLink(int mId, string url)
    {
        MId = mId;
        Url = url;
    }

    [Required]
    public int MId { get; set; }

    [Required]
    public string Url { get; set; }
}
