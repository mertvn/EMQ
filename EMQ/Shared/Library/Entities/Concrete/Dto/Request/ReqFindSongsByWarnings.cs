using System.ComponentModel.DataAnnotations;
using EMQ.Shared.Quiz.Entities.Concrete;

namespace EMQ.Shared.Library.Entities.Concrete.Dto.Request;

public class ReqFindSongsByWarnings
{
    public ReqFindSongsByWarnings(MediaAnalyserWarningKind[] warnings, SongSourceSongTypeMode mode)
    {
        Warnings = warnings;
        Mode = mode;
    }

    [Required]
    public MediaAnalyserWarningKind[] Warnings { get; }

    [Required]
    public SongSourceSongTypeMode Mode { get; }
}
