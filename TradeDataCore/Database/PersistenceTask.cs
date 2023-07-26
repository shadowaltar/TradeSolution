using TradeCommon.Essentials;
using TradeCommon.Essentials.Instruments;
using TradeCommon.Essentials.Quotes;

namespace TradeDataCore.Database;

public class PersistenceTask<T> : IPersistenceTask
{
    public SecurityType SecurityType { get; set; }

    public List<T> Entries { get; internal set; }
    public string TableName { get; internal set; }
    public string DatabaseName { get; internal set; }
}

public class OhlcPricePersistenceTask : PersistenceTask<OhlcPrice>
{
    public int SecurityId { get; set; }
    public IntervalType IntervalType { get; set; }
}