namespace Juliet.Model.Param;

public class Param
{
    /// <summary>
    /// VNDB API token <br/>
    /// Used in: GET_authinfo, POST_ulist <br/>
    /// </summary>
    public string? APIToken { get; set; }

    /// <summary>
    /// vndbid or username <br/>
    /// Does NOT accept bare integers as users can have number-only usernames. <br/>
    /// Used in: GET_user, POST_ulist <br/>
    /// </summary>
    public string? User { get; set; }
}
