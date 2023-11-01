using System.Runtime.CompilerServices;

namespace Common;
public static class ObjectExtensions
{
    /// <summary>
    /// (Safely) compare two objects.
    /// Notice that non-null object is always larger than the null object.
    /// </summary>
    /// <param name="o1"></param>
    /// <param name="o2"></param>
    /// <returns></returns>
    public static int SafeCompareTo<T>(this T? o1, T? o2) where T : IComparable
    {
        return o1 == null && o2 == null ? 0 : o1 != null && o2 == null ? 1 : o1 == null && o2 != null ? -1 : o1!.CompareTo(o2);
    }

    /// <summary>
    /// Unsafe cast to type <typeparamref name="T"/>.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="o"></param>
    /// <returns></returns>
    public static T As<T>(this object o)
    {
        return Unsafe.As<object, T>(ref o);
    }
}
