using TradeCommon.Essentials.Instruments;
using TradeCommon.Essentials.Trading;

namespace TradeLogicCore.Services;

public interface ITradeService
{
    /// <summary>
    /// Event invoked when a single trade is received.
    /// </summary>
    event Action<Trade>? NextTrade;

    /// <summary>
    /// Event invoked when a list of trades are received simultaneously.
    /// For example, an order matches multiple depths in an order book
    /// will result in multiple trades.
    /// </summary>    
    event Action<List<Trade>>? NextTrades;

    IReadOnlyDictionary<long, long> TradeToOrderIds { get; }

    Task<List<Trade>?> GetMarketTrades(Security security);

    Task<List<Trade>?> GetTrades(Security security);

    Task<List<Trade>?> GetTradeHistory(DateTime start, DateTime end, Security security, bool requestExternal = false);
}