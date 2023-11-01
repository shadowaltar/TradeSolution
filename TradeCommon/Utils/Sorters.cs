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
            return x == null && y == null ? 0 : x == null ? -1 : y == null ? 1 : x.SecurityCode.CompareTo(y.SecurityCode);
        }
    }
    public class IdSorterImpl : IComparer<SecurityRelatedEntry>
    {
        public int Compare(SecurityRelatedEntry? x, SecurityRelatedEntry? y)
        {
            return x == null && y == null ? 0 : x == null ? -1 : y == null ? 1 : x.SecurityId.CompareTo(y.SecurityId);
        }
    }
    public class CreateTimeSorterImpl : IComparer<ITimeRelatedEntry>
    {
        public int Compare(ITimeRelatedEntry? x, ITimeRelatedEntry? y)
        {
            return x == null && y == null ? 0 : x == null ? -1 : y == null ? 1 : x.CreateTime.CompareTo(y.CreateTime);
        }
    }
}
