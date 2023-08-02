using System.Collections.Generic;

namespace EMQ.Shared.Library.Entities.Concrete.Dto.Request;

public class ReqSongReport
{
    public ReqSongReport(SongReport songReport, Dictionary<string, bool> selectedUrls)
    {
        SongReport = songReport;
        SelectedUrls = selectedUrls;
    }

    public SongReport SongReport { get; }

    public Dictionary<string, bool> SelectedUrls { get; }
}
