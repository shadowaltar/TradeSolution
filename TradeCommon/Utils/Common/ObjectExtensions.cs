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
        if (o1 == null && o2 == null) return 0;
        if (o1 != null && o2 == null) return 1;
        if (o1 == null && o2 != null) return -1;

        return o1!.CompareTo(o2);
    }
}
