using Common;
using TradeCommon.Runtime;

namespace TradeLogicCore.Algorithms;
public class WorkingItemMonitor<T> where T : SecurityRelatedEntry, IIdEntry
{
    public Dictionary<int, T> ItemsBySecurityId { get; } = [];

    /// <summary>
    /// If an item is being monitored, returns true,
    /// otherwise start to monitor this item.
    /// </summary>
    /// <param name="item"></param>
    /// <returns></returns>
    public bool MonitorAndPreventOtherActivity(T item)
    {
        return item.IsSecurityInvalid()
            ? throw Exceptions.InvalidSecurityInPosition(item.Id)
            : ItemsBySecurityId.ThreadSafeContainsOrSet(item.SecurityId, item);
    }

    /// <summary>
    /// Checks if an item by its security id is being monitored.
    /// Returns true if yes.
    /// </summary>
    /// <param name="securityId"></param>
    /// <returns></returns>
    public bool IsMonitoring(int securityId)
    {
        return ItemsBySecurityId.ThreadSafeContains(securityId);
    }

    /// <summary>
    /// Marks an item by its security id as done, and remove it from the monitoring list.
    /// Returns true when the item was there, false when the item was not.
    /// </summary>
    /// <param name="securityId"></param>
    /// <returns></returns>
    public bool MarkAsDone(int securityId)
    {
        return ItemsBySecurityId.ThreadSafeRemove(securityId);
    }
}
