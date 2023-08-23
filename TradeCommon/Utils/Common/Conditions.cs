namespace TradeCommon.Utils.Common;
public static class Conditions
{
    public static bool AnyNull<T>(T v1, T v2)
    {
        return (v1 == null && v2 != null) || (v1 != null && v2 == null);
    }

    public static bool AllNotNull<T>(params T[] vs)
    {
        for (int i = 0; i < vs.Length; i++)
        {
            if (vs[i] == null) return false;
        }
        return true;
    }

    public static bool AllNull<T>(params T[] vs)
    {
        for (int i = 0; i < vs.Length; i++)
        {
            if (vs[i] != null) return false;
        }
        return true;
    }
}

