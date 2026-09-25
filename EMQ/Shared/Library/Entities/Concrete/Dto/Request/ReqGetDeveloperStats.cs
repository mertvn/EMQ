using EMQ.Shared.Quiz.Entities.Concrete;

namespace EMQ.Shared.Library.Entities.Concrete.Dto.Request;

public class ReqGetDeveloperStats
{
    public SongSourceType SourceType { get; set; } = SongSourceType.VN;

    public string DeveloperId { get; set; } = "";

    public string Name { get; set; } = "";
}
