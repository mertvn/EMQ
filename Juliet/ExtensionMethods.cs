using System.ComponentModel;
using System.Reflection;

namespace Juliet;

public static class ExtensionMethods
{
    // Falls back to name
    public static string? GetDescription(this Enum value)
    {
        Type type = value.GetType();
        string? name = Enum.GetName(type, value);
        if (name != null)
        {
            FieldInfo? field = type.GetField(name);
            if (field != null)
            {
                if (Attribute.GetCustomAttribute(field,
                        typeof(DescriptionAttribute)) is DescriptionAttribute attr)
                {
                    return attr.Description;
                }
                else
                {
                    return name;
                }
            }
        }

        return null;
    }
}
