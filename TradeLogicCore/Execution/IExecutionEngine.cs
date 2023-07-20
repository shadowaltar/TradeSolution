using TradeCommon.Essentials;
using TradeCommon.Essentials.Trading;

namespace TradeLogicCore.Execution;

/// <summary>
/// Trade execution engine interface. Implement this for each
/// of the broker / exchange.
/// </summary>
public interface IExecutionEngine
{
    bool Initialize(User user);

    void PlaceOrder(Order order);

    void CancelOrder(Order order);

    void ModifyOrder(Order order);

    void CancelAllOrder(Order order);

    event OrderPlacedCallback OrderPlaced;
    event OrderModifiedCallback OrderModified;
    event OrderCanceledCallback OrderCanceled;
    event AllOrderCanceledCallback AllOrderCanceled;
}

public delegate void OrderPlacedCallback(bool isSuccessful, Order order);
public delegate void OrderModifiedCallback(bool isSuccessful, Order orderSent, Order orderReceived);
public delegate void OrderCanceledCallback(bool isSuccessful, Order order);
public delegate void AllOrderCanceledCallback(bool isSuccessful, IList<Order> orders);
