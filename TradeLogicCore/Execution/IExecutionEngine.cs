using TradeCommon.Essentials.Trading;

namespace TradeLogicCore.Execution;

/// <summary>
/// Trade execution engine interface. Implement this for each
/// of the broker / exchange.
/// </summary>
public interface IExecutionEngine
{
    void PlaceOrder(Order order);

    void CancelOrder(Order order);
    
    void ModifyOrder(Order order);

    void CancelAllOrder(Order order);
}