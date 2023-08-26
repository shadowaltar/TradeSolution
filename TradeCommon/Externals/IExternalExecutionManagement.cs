using TradeCommon.Essentials.Accounts;
using TradeCommon.Essentials.Instruments;
using TradeCommon.Essentials.Trading;
using TradeCommon.Runtime;
using static TradeCommon.Utils.Delegates;

namespace TradeCommon.Externals;
public interface IExternalExecutionManagement
{
    bool IsFakeOrderSupported { get; }

    event OrderPlacedCallback? OrderPlaced;
    event OrderModifiedCallback? OrderModified;
    event OrderCancelledCallback? OrderCancelled;
    event AllOrderCancelledCallback? AllOrderCancelled;
    event TradeReceivedCallback? TradeReceived;
    event TradesReceivedCallback? TradesReceived;

    Task<ExternalQueryState<Order>> SendOrder(Order order);

    Task<ExternalQueryState<Order>> CancelOrder(Order order);

    Task<ExternalQueryState<Order>> GetOrder(Security security, long orderId = 0, long externalOrderId = 0);

    Task<ExternalQueryState<List<Order>?>> GetOpenOrders(Security? security = null);

    Task<ExternalQueryState<List<Order>?>> GetOrderHistory(DateTime start, DateTime end);

    Task<ExternalQueryState<Order>> UpdateOrder(Order order);

    Task<ExternalQueryState<List<Order>>> CancelAllOrders(Security security);

    Task<ExternalQueryState<int>> GetOrderSpeedLimit();

    /// <summary>
    /// Get all the (recent) trades visible in the market with a given security.
    /// </summary>
    /// <returns></returns>
    Task<ExternalQueryState<List<Trade>?>> GetMarketTrades(Security security);
}
