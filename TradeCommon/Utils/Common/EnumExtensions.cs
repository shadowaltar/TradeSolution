using System.ComponentModel;
using System.Reflection;
using TradeCommon.Essentials.Trading;

namespace Common;
public static class EnumExtensions
{
    private static readonly Dictionary<Type, Dictionary<string, object>> _descriptionToEnumValues = [];
    private static readonly Dictionary<Type, Dictionary<object, string>> _enumValueToDescriptions = [];
    private static readonly Type _descriptionType = typeof(DescriptionAttribute);

    public static T ConvertDescriptionToEnum<T>(this string description, T defaultValue = default!) where T : Enum
    {
        return (T)ConvertDescriptionToEnum(description, typeof(T), defaultValue ?? default(T) ?? default!);
    }

    public static string ConvertEnumToDescription<T>(this T enumValue, string defaultValue = "") where T : Enum
    {
        var type = typeof(T);
        if (!_enumValueToDescriptions.TryGetValue(type, out var values))
        {
            Cache(type);
            values = _enumValueToDescriptions[type];
        }
        return values.TryGetValue(enumValue, out var str) ? str : defaultValue;
    }

    public static object ConvertDescriptionToEnum(this string description, Type type, object defaultValue)
    {
        if (!_descriptionToEnumValues.TryGetValue(type, out var values))
        {
            Cache(type);
            values = _descriptionToEnumValues[type];
        }
        return values.TryGetValue(description, out var value) ? value : defaultValue;
    }

    private static void Cache(Type type)
    {
        var d2e = new Dictionary<string, object>();
        var e2d = new Dictionary<object, string>();
        foreach (var field in type.GetFields(BindingFlags.Public | BindingFlags.Static))
        {
            var enumValue = field.GetValue(null);
            if (enumValue == null)
                continue;
            var enumValueStr = enumValue.ToString()!;
            d2e[enumValueStr] = enumValue;
            d2e[enumValueStr.ToUpperTrimmed()] = enumValue;
            d2e[enumValueStr.ToLowerTrimmed()] = enumValue;
            if (Attribute.GetCustomAttribute(field, _descriptionType) is DescriptionAttribute attribute)
            {
                var descriptions = attribute.Description.Split('|');

                foreach (var desc in descriptions)
                {
                    d2e[desc] = enumValue;
                }
                e2d[enumValue] = descriptions[0];
            }
            else
            {

                e2d[enumValue] = enumValueStr;
            }
        }
        _descriptionToEnumValues[type] = d2e;
        _enumValueToDescriptions[type] = e2d;
    }

    public static T ParseEnum<T>(this string value) where T : struct, Enum
    {
        return value == null ? default : Enum.TryParse<T>(value, out var t) ? t : default;
    }

    public static bool IsUnknown<T>(this T value) where T : struct, Enum
    {
        return value.As<int>() == 0;
    }

    public static Side Invert(this Side side)
    {
        return side switch
        {
            Side.Buy => Side.Sell,
            Side.Sell => Side.Buy,
            _ => Side.None,
        };
    }
}
