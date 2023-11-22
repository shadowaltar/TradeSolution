using TradeCommon.Essentials.Instruments;
using TradeCommon.Essentials.Trading;
using TradeCommon.Runtime;
using static TradeCommon.Utils.Delegates;

namespace TradeCommon.Externals;
public interface IExternalExecutionManagement
{
    event OrderPlacedCallback? OrderPlaced;
    event OrderModifiedCallback? OrderModified;
    event OrderCancelledCallback? OrderCancelled;
    event AllOrderCancelledCallback? AllOrderCancelled;
    event OrderReceivedCallback? OrderReceived;
    event TradeReceivedCallback? TradeReceived;

    event AssetsChangedCallback? AssetsChanged;
    event TransferredCallback? Transferred;

    Task<ExternalConnectionState> Subscribe();
    
    Task<ExternalQueryState> Unsubscribe();

    Task<ExternalQueryState> SendOrder(Order order);

    Task<ExternalQueryState> CancelOrder(Order order);

    Task<ExternalQueryState> GetOrder(Security security, long orderId = 0, long externalOrderId = 0);

    Task<ExternalQueryState> GetOpenOrders(Security? security = null);

    Task<ExternalQueryState> GetOrders(Security security,
                                       long oldestExternalOrderId = long.MinValue,
                                       DateTime? start = null,
                                       DateTime? end = null);

    Task<ExternalQueryState> UpdateOrder(Order order);

    Task<ExternalQueryState> CancelAllOrders(Security security);

    Task<ExternalQueryState> GetOrderSpeedLimit();

    /// <summary>
    /// Get all the (recent) trades visible in the market with a given security.
    /// </summary>
    /// <returns></returns>
    Task<ExternalQueryState> GetMarketTrades(Security security);

    Task<ExternalQueryState> GetTrades(Security security,
                                       long oldestExternalOrderId = long.MinValue,
                                       DateTime? start = null,
                                       DateTime? end = null);

    /// <summary>
    /// Get the asset positions related to an account.
    /// </summary>
    /// <param name="account"></param>
    /// <returns></returns>
    Task<ExternalQueryState> GetAssetPositions(string accountId);
}
