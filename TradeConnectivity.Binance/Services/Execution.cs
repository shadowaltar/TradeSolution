using TradeCommon.Essentials;
using TradeCommon.Essentials.Trading;
using TradeCommon.Externals;
using static TradeCommon.Utils.Delegates;

namespace TradeConnectivity.Binance.Services;
public class Execution : IExternalExecutionManagement
{
    public event OrderPlacedCallback? OrderPlaced;
    public event OrderModifiedCallback? OrderModified;
    public event OrderCanceledCallback? OrderCanceled;
    public event AllOrderCanceledCallback? AllOrderCanceled;
    public event TradeReceivedCallback? TradeReceived;

    public void CancelAllOrder(Order order)
    {
        throw new NotImplementedException();
    }

    public void CancelOrder(Order order)
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

    public void PlaceOrder(Order order)
    {
        throw new NotImplementedException();
    }
}
