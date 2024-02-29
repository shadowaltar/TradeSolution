using TradeCommon.Essentials.Instruments;
using TradeCommon.Essentials.Portfolios;
using TradeCommon.Essentials.Trading;
using static TradeCommon.Utils.Delegates;

namespace TradeLogicCore.Services;

public interface ITradeService
{
    /// <summary>
    /// Event invoked when a single trade is received.
    /// </summary>
    event TradeReceivedCallback? TradeProcessed;

    /// <summary>
    /// Event invoked when a list of trades are received simultaneously.
    /// For example, an order matches multiple depths in an order book
    /// will result in multiple trades.
    /// The bool flag indicates whether all trades are of the same security.
    /// </summary>    
    event TradesReceivedCallback? NextTrades;

    void Initialize();

    /// <summary>
    /// Get the recent trades executed in the market (not only ours).
    /// </summary>
    /// <param name="security"></param>
    /// <returns></returns>
    Task<List<Trade>> GetMarketTrades(Security security);

    Task<List<Trade>> GetExternalTrades(Security security, DateTime? start = null, DateTime? end = null);

    Task<List<Trade>> GetStorageTrades(Security security, DateTime? start = null, DateTime? end = null, bool? isOperational = false);

    /// <summary>
    /// Get the executed trades for a given security.
    /// </summary>
    /// <param name="security"></param>
    /// <param name="start"></param>
    /// <param name="end"></param>
    /// <returns></returns>
    List<Trade> GetTrades(Security security, DateTime? start = null, DateTime? end = null);

    /// <summary>
    /// Get the executed trades by their order.
    /// </summary>
    /// <param name="orderId"></param>
    /// <returns></returns>
    List<Trade> GetTradesByOrderId(long orderId);

    /// <summary>
    /// Get the trades related to an order.
    /// </summary>
    /// <param name="security"></param>
    /// <param name="orderId"></param>
    /// <param name="requestExternal"></param>
    /// <returns></returns>
    Task<List<Trade>> GetTrades(Security security, long orderId, bool requestExternal = false);

    /// <summary>
    /// Get the cached trades related to an order.
    /// </summary>
    /// <param name="orderId"></param>
    /// <returns></returns>
    List<Trade> GetTrades(long orderId);

    /// <summary>
    /// Reset all caches in this service.
    /// </summary>
    void Reset();

    /// <summary>
    /// Update the internal state.
    /// </summary>
    /// <param name="trades"></param>
    /// <param name="security"></param>
    void Update(ICollection<Trade> trades, Security? security = null);
}