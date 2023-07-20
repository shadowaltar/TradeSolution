using TradeCommon.Essentials;
using TradeCommon.Essentials.Trading;

namespace TradeLogicCore.Execution;
public class FutuEngine : IExecutionEngine
{
    public event OrderPlacedCallback OrderPlaced;
    public event OrderModifiedCallback OrderModified;
    public event OrderCanceledCallback OrderCanceled;
    public event AllOrderCanceledCallback AllOrderCanceled;

    public bool Initialize(User user)
    {
        throw new NotImplementedException();
    }

    public void CancelAllOrder(Order order)
    {
        throw new NotImplementedException();
    }

    public void CancelOrder(Order order)
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
