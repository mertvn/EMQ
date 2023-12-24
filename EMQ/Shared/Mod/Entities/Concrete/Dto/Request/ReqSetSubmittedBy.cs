using System.ComponentModel.DataAnnotations;

namespace EMQ.Shared.Mod.Entities.Concrete.Dto.Request;

public class ReqSetSubmittedBy
{
    public ReqSetSubmittedBy(string[] urls, string submittedBy)
    {
        Urls = urls;
        SubmittedBy = submittedBy;
    }

    [Required]
    public string[] Urls { get; set; }

    [Required]
    public string SubmittedBy { get; set; }
}
