using TradeCommon.Runtime;

namespace TradeCommon.Utils;

public class Sorters
{
    public static IComparer<SecurityRelatedEntry> CodeSorter { get; } = new CodeSorterImpl();
    public static IComparer<SecurityRelatedEntry> IdSorter { get; } = new IdSorterImpl();
    public static IComparer<ITimeRelatedEntry> CreateTimeSorter { get; } = new CreateTimeSorterImpl();

    public class CodeSorterImpl : IComparer<SecurityRelatedEntry>
    {
        public int Compare(SecurityRelatedEntry? x, SecurityRelatedEntry? y)
        {
            if (x == null && y == null) return 0;
            if (x == null) return -1;
            if (y == null) return 1;
            return x.SecurityCode.CompareTo(y.SecurityCode);
        }
    }
    public class IdSorterImpl : IComparer<SecurityRelatedEntry>
    {
        public int Compare(SecurityRelatedEntry? x, SecurityRelatedEntry? y)
        {
            if (x == null && y == null) return 0;
            if (x == null) return -1;
            if (y == null) return 1;
            return x.SecurityId.CompareTo(y.SecurityId);
        }
    }
    public class CreateTimeSorterImpl : IComparer<ITimeRelatedEntry>
    {
        public int Compare(ITimeRelatedEntry? x, ITimeRelatedEntry? y)
        {
            if (x == null && y == null) return 0;
            if (x == null) return -1;
            if (y == null) return 1;
            return x.CreateTime.CompareTo(y.CreateTime);
        }
    }
}
