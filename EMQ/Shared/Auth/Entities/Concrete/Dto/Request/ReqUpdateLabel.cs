using EMQ.Shared.Quiz.Entities.Concrete;

namespace EMQ.Shared.Auth.Entities.Concrete.Dto.Request;

public class ReqUpdateLabel
{
    public ReqUpdateLabel(string playerToken, Label label, UserListDatabaseKind databaseKind)
    {
        PlayerToken = playerToken;
        Label = label;
        DatabaseKind = databaseKind;
    }

    public string PlayerToken { get; }

    public Label Label { get; }

    public UserListDatabaseKind DatabaseKind { get; }
}
