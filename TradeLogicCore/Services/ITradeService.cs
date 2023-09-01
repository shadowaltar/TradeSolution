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
    event Action<Trade[]>? NextTrades;

    IReadOnlyDictionary<long, long> TradeToOrderIds { get; }

    /// <summary>
    /// Get the recent trades executed in the market (not only ours).
    /// </summary>
    /// <param name="security"></param>
    /// <returns></returns>
    Task<Trade[]?> GetMarketTrades(Security security);

    /// <summary>
    /// Get the executed trades for a given security.
    /// </summary>
    /// <param name="security"></param>
    /// <param name="start"></param>
    /// <param name="end"></param>
    /// <param name="requestExternal"></param>
    /// <returns></returns>
    Task<Trade[]?> GetTrades(Security security, DateTime? start = null, DateTime? end = null, bool requestExternal = false);

    /// <summary>
    /// Get the trades related to an order.
    /// </summary>
    /// <param name="security"></param>
    /// <param name="orderId"></param>
    /// <param name="requestExternal"></param>
    /// <returns></returns>
    Task<Trade[]?> GetTrades(Security security, long orderId, bool requestExternal = false);
}