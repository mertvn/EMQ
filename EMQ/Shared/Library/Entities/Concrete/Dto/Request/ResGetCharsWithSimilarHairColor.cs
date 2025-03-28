using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace EMQ.Shared.Library.Entities.Concrete.Dto.Request;

public record ReqGetCharsWithSimilarHairColor
{
    public string TargetId { get; set; } = "";

    [Range(2, 500)]
    public int TopN { get; set; }

    public List<string> ValidIds { get; set; } = new();
}
