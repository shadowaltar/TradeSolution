using System.ComponentModel;
using System.Reflection;

namespace TradeDataCore.Utils;

public static class EnumExtensions
{
    private static readonly Dictionary<Type, Dictionary<string, object>> _enumValues = new();
    private static readonly Type _descriptionType = typeof(DescriptionAttribute);

    public static T ConvertDescriptionToEnum<T>(this string description, T defaultValue = default!) where T : Enum
    {
        return (T)ConvertDescriptionToEnum(description, typeof(T), defaultValue ?? default(T) ?? default!);
    }

    public static object ConvertDescriptionToEnum(this string description, Type type, object defaultValue)
    {
        if (!_enumValues.TryGetValue(type, out var values))
        {
            var mapping = new Dictionary<string, object>();
            foreach (var field in type.GetFields(BindingFlags.Public | BindingFlags.Static))
            {
                var enumValue = field.GetValue(null);
                if (enumValue == null)
                    continue;
                mapping[enumValue.ToString()] = enumValue;
                mapping[enumValue.ToString().ToUpperInvariant()] = enumValue;
                if (Attribute.GetCustomAttribute(field, _descriptionType) is DescriptionAttribute attribute)
                {
                    var descriptions = attribute.Description.Split('|');

                    foreach (var desc in descriptions)
                        mapping[desc] = enumValue;
                }
            }
            values = _enumValues[type] = mapping;
        }

        return values.TryGetValue(description, out var value) ? value : defaultValue;
    }

    public static T ParseEnum<T>(this string value) where T : struct, Enum
    {
        if (value == null) return default;
        return Enum.TryParse<T>(value, out var t) ? t : default;
    }
}
