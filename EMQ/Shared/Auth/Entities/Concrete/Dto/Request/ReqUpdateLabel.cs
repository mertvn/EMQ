using EMQ.Shared.Quiz.Entities.Concrete;

namespace EMQ.Shared.Auth.Entities.Concrete.Dto.Request;

public class ReqUpdateLabel
{
    public ReqUpdateLabel(string playerToken, Label label)
    {
        PlayerToken = playerToken;
        Label = label;
    }

    public string PlayerToken { get; }

    public Label Label { get; }
}
