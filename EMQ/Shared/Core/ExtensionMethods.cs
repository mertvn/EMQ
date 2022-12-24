namespace EMQ.Shared.Core;

public static class ExtensionMethods
{
    public static string Base64Encode(this string plainText)
    {
        byte[] plainTextBytes = System.Text.Encoding.UTF8.GetBytes(plainText);
        return System.Convert.ToBase64String(plainTextBytes);
    }

    public static string Base64Decode(this string base64EncodedData)
    {
        byte[] base64EncodedBytes = System.Convert.FromBase64String(base64EncodedData);
        return System.Text.Encoding.UTF8.GetString(base64EncodedBytes);
    }

    public static bool IsVideoLink(this string s)
    {
        return s.EndsWith(".webm") || s.EndsWith(".mp4");
    }

    public static string ToVndbUrl(this string? vndbId)
    {
        return "https://vndb.org/" + vndbId;
    }

    public static string ToVndbId(this string vndbUrl)
    {
        return vndbUrl.Replace("https://vndb.org/", "");
    }
}
