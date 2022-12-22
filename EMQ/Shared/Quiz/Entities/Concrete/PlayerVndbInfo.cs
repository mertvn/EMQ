using System.Collections.Generic;

namespace EMQ.Shared.Quiz.Entities.Concrete;

public class PlayerVndbInfo
{
    public string? VndbId { get; set; }

    public string? VndbApiToken { get; set; }

    public List<string>? VNs { get; set; }
}
