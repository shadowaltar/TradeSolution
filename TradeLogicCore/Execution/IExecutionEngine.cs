using TradeLogicCore.Essentials;

namespace TradeLogicCore.Execution;

public interface IExecutionEngine
{
    void PlaceOrder(Order order);

    void CancelOrder(Order order);
    
    void ModifyOrder(Order order);

    void CancelAllOrder(Order order);
}