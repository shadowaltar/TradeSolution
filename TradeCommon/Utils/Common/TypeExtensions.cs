using Common.Attributes;
using System.Reflection;

namespace Common;

public static class TypeExtensions
{
    private static readonly FieldNameEqualityComparer _comparer = new();

    public static List<T> GetDistinctAttributes<T>(this Type type, IEqualityComparer<T>? comparer = null) where T : Attribute
    {
        if (comparer != null)
            return type.GetCustomAttributes<T>().Distinct(comparer).ToList();

        if (typeof(T) is IFieldNamesAttribute)
            return type.GetCustomAttributes<T>().OfType<IFieldNamesAttribute>()
                .Distinct(_comparer).OfType<T>().ToList();

        return type.GetCustomAttributes<T>().Distinct().ToList();
    }
}
