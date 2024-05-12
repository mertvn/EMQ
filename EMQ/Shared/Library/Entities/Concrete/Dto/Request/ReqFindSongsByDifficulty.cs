using System.ComponentModel.DataAnnotations;
using EMQ.Shared.Quiz.Entities.Concrete;

namespace EMQ.Shared.Library.Entities.Concrete.Dto.Request;

public class ReqFindSongsByDifficulty
{
    public ReqFindSongsByDifficulty(SongDifficultyLevel difficulty, SongSourceSongTypeMode mode)
    {
        Difficulty = difficulty;
        Mode = mode;
    }

    [Required]
    public SongDifficultyLevel Difficulty { get; }

    [Required]
    public SongSourceSongTypeMode Mode { get; }
}
