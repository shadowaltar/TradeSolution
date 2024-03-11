using Common.Attributes;
using System.ComponentModel.DataAnnotations;

namespace Common;
public static class Validator
{
    public static readonly List<Type> ValidationAttributes =
    [
        typeof(PositiveAttribute),
        typeof(NotNegativeAttribute),
        typeof(NotUnknownAttribute),
        typeof(MinLengthAttribute)
    ];

    public static readonly List<Type> AutoCorrectAttributes =
    [
        typeof(AlwaysUpperCaseAttribute),
        typeof(AlwaysLowerCaseAttribute),
    ];

    /// <summary>
    /// Validate an instance and if not ok, throws <see cref="ArgumentException"/>.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="targetObject"></param>
    /// <exception cref="ArgumentException"></exception>
    public static void ValidateOrThrow<T>(this T targetObject)
    {
        var vg = ReflectionUtils.GetValueGetter<T>();
        if (!vg.Validate(targetObject, out var propName, out var ruleName))
            throw new ArgumentException($"Failed validation for property {propName} in type {typeof(T).Name} against rule {ruleName}");
    }

    public static void AutoCorrect<T>(this T targetObject)
    {
        var vs = ReflectionUtils.GetValueSetter<T>();
        var vg = ReflectionUtils.GetValueGetter<T>();
        foreach (var name in vs.GetNames())
        {
            if (vg.IsWithAutoCorrect(targetObject, name, out var original, out var attr))
            {
                var now = attr.AutoCorrect(original);
                vs.Set(targetObject, name, now);
            }
        }
    }
}
