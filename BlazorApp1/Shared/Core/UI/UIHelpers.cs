namespace BlazorApp1.Shared.Core.UI;

public static class UiHelpers
{
    public static string Bool2Vis(bool b) => b ? "visible" : "hidden";

    public static string Bool2Color(bool? b, string trueValue, string falseValue, string nullValue = "")
    {
        if (b is null)
        {
            return nullValue;
        }
        else
        {
            return (bool)b ? trueValue : falseValue;
        }
    }

    public static string Bool2PointerEvents(bool b) => b ? "auto" : "none";

    public static string Bool2Text(bool? b, string trueValue, string falseValue, string nullValue = "")
    {
        if (b is null)
        {
            return nullValue;
        }
        else
        {
            return (bool)b ? trueValue : falseValue;
        }
    }
}
