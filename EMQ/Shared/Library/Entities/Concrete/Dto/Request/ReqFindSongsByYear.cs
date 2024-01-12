using System.ComponentModel.DataAnnotations;
using EMQ.Shared.Quiz.Entities.Concrete;

namespace EMQ.Shared.Library.Entities.Concrete.Dto.Request;

public class ReqFindSongsByYear
{
    public ReqFindSongsByYear(int year, SongSourceSongTypeMode mode)
    {
        Year = year;
        Mode = mode;
    }

    [Required]
    public int Year { get; }

    [Required]
    public SongSourceSongTypeMode Mode { get; }
}
