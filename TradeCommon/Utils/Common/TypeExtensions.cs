using Common.Attributes;
using System.Reflection;

namespace Common;

public static class TypeExtensions
{
    private static readonly FieldNameEqualityComparer _comparer = new();

    public static List<T> GetDistinctAttributes<T>(this Type type, IEqualityComparer<T>? comparer = null) where T : Attribute
    {
        return comparer != null
            ? type.GetCustomAttributes<T>().Distinct(comparer).ToList()
            : typeof(T) is IFieldNamesAttribute
            ? type.GetCustomAttributes<T>().OfType<IFieldNamesAttribute>()
                .Distinct(_comparer).OfType<T>().ToList()
            : type.GetCustomAttributes<T>().Distinct().ToList();
    }
}
