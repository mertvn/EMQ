using System.Collections.Generic;

namespace EMQ.Shared.Library.Entities.Concrete.Dto.Request;

public class ReqFindSongsByMBIDs
{
    public ReqFindSongsByMBIDs(List<string> mbids)
    {
        MBIDs = mbids;
    }

    public List<string> MBIDs { get; }
}
