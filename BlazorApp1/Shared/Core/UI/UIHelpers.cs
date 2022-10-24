namespace BlazorApp1.Shared.Core.UI;

public static class UiHelpers
{
    public static string Bool2Vis(bool b) => b ? "visible" : "hidden";

    public static string Bool2Color(bool? b, string trueColor, string falseColor, string nullColor = "")
    {
        if (b is null)
        {
            return nullColor;
        }
        else
        {
            return (bool)b ? trueColor : falseColor;
        }
    }

    public static string Bool2PointerEvents(bool b) => b ? "auto" : "none";
}
