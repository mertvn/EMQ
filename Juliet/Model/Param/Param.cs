namespace Juliet.Model.Param;

public class Param
{
    /// <summary>
    /// VNDB API token <br/>
    /// Used in: GET_authinfo, GET_ulist_labels, POST_ulist, POST_vn <br/>
    /// </summary>
    public string? APIToken { get; set; }

    /// <summary>
    /// vndbid or username <br/>
    /// Does NOT accept bare integers as users can have number-only usernames. <br/>
    /// Used in: GET_user, GET_ulist_labels, POST_ulist, POST_vn <br/>
    /// </summary>
    public string? User { get; set; }
}
