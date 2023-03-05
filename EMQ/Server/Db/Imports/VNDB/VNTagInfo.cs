using System.Collections.Generic;

namespace EMQ.Server.Db.Imports.VNDB;

public class VNTagInfo
{
    public string VNID { get; set; } = "";

    public List<TVI> TVIs { get; set; } = new();
}
