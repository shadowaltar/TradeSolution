using TradeCommon.Essentials;
using TradeCommon.Essentials.Instruments;
using TradeCommon.Essentials.Trading;
using TradeCommon.Externals;
using static TradeCommon.Utils.Delegates;

namespace TradeConnectivity.Futu.Services;
public class Execution : IExternalExecutionManagement
{
    public bool IsFakeOrderSupported => throw new NotImplementedException();

    public event OrderPlacedCallback? OrderPlaced;
    public event OrderModifiedCallback? OrderModified;
    public event OrderCancelledCallback? OrderCancelled;
    public event AllOrderCancelledCallback? AllOrderCancelled;
    public event TradeReceivedCallback? TradeReceived;
    public event TradesReceivedCallback? TradesReceived;

    public Task CancelAllOrder(Order order)
    {
        throw new NotImplementedException();
    }

    public Task CancelOrder(Order order)
    {
        throw new NotImplementedException();
    }

    public Task<List<Trade>?> GetMarketTrades(Security security)
    {
        throw new NotImplementedException();
    }

    public Task<bool> Initialize(User user)
    {
        throw new NotImplementedException();
    }

    public Task ModifyOrder(Order order)
    {
        throw new NotImplementedException();
    }

    public Task SendOrder(Order order)
    {
        throw new NotImplementedException();
    }
}
