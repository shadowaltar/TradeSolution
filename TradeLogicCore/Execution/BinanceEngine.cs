using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TradeCommon.Essentials;
using TradeCommon.Essentials.Trading;

namespace TradeLogicCore.Execution;
public class BinanceEngine : IExecutionEngine
{
    public event OrderPlacedCallback OrderPlaced;
    public event OrderModifiedCallback OrderModified;
    public event OrderCanceledCallback OrderCanceled;
    public event AllOrderCanceledCallback AllOrderCanceled;

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
