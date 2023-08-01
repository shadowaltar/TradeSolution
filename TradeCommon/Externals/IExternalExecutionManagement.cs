using TradeCommon.Essentials;
using TradeCommon.Essentials.Instruments;
using TradeCommon.Essentials.Trading;
using static TradeCommon.Utils.Delegates;

namespace TradeCommon.Externals;
public interface IExternalExecutionManagement
{
    Task<bool> Initialize(User user);

    Task SendOrder(Order order);

    Task CancelOrder(Order order);

    Task ModifyOrder(Order order);

    Task CancelAllOrder(Order order);

    /// <summary>
    /// Get all the (recent) trades visible in the market with a given security.
    /// </summary>
    /// <returns></returns>
    Task<List<Trade>?> GetMarketTrades(Security security);
    Task<List<Account>> GetAccountDetails();

    event OrderPlacedCallback? OrderPlaced;
    event OrderModifiedCallback? OrderModified;
    event OrderCancelledCallback? OrderCancelled;
    event AllOrderCancelledCallback? AllOrderCancelled;
    event TradeReceivedCallback? TradeReceived;
    event TradesReceivedCallback? TradesReceived;
}
