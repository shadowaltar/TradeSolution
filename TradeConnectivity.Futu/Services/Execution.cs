using TradeCommon.Essentials;
using TradeCommon.Essentials.Instruments;
using TradeCommon.Essentials.Trading;
using TradeCommon.Externals;
using static TradeCommon.Utils.Delegates;

namespace TradeConnectivity.Futu.Services;
public class Execution : IExternalExecutionManagement
{
    public event OrderPlacedCallback? OrderPlaced;
    public event OrderModifiedCallback? OrderModified;
    public event OrderCanceledCallback? OrderCanceled;
    public event AllOrderCanceledCallback? AllOrderCanceled;
    public event TradeReceivedCallback? TradeReceived;
    public event TradesReceivedCallback? TradesReceived;

    public void CancelAllOrder(Order order)
    {
        throw new NotImplementedException();
    }

    public void CancelOrder(Order order)
    {
        throw new NotImplementedException();
    }

    public Task<List<Trade>?> GetMarketTrades(Security security)
    {
        throw new NotImplementedException();
    }

    public bool Initialize(User user)
    {
        throw new NotImplementedException();
    }

    public void ModifyOrder(Order order)
    {
        throw new NotImplementedException();
    }

    public void SendOrder(Order order)
    {
        throw new NotImplementedException();
    }
}
