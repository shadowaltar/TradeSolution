using TradeCommon.Essentials;
using TradeCommon.Essentials.Instruments;
using TradeCommon.Essentials.Trading;
using static TradeCommon.Utils.Delegates;

namespace TradeCommon.Externals;
public interface IExternalExecutionManagement
{
    bool Initialize(User user);

    void SendOrder(Order order);

    void CancelOrder(Order order);

    void ModifyOrder(Order order);

    void CancelAllOrder(Order order);

    /// <summary>
    /// Get all the (recent) trades visible in the market with a given security.
    /// </summary>
    /// <returns></returns>
    Task<List<Trade>?> GetMarketTrades(Security security);

    event OrderPlacedCallback? OrderPlaced;
    event OrderModifiedCallback? OrderModified;
    event OrderCanceledCallback? OrderCanceled;
    event AllOrderCanceledCallback? AllOrderCanceled;
    event TradeReceivedCallback? TradeReceived;
    event TradesReceivedCallback? TradesReceived;
}
