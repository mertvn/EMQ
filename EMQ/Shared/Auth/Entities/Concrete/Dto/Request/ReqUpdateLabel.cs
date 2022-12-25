using EMQ.Shared.Quiz.Entities.Concrete;

namespace EMQ.Shared.Auth.Entities.Concrete.Dto.Request;

public class ReqUpdateLabel
{
    public ReqUpdateLabel(string token, Label label)
    {
        Token = token;
        Label = label;
    }

    public string Token { get; }

    public Label Label { get; }
}
