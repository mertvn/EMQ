using System.Collections.Generic;

namespace EMQ.Server.Db.Imports;

public class VNTagInfo
{
    public string VNID { get; set; }

    public List<TVI> TVIs { get; set; }
}
