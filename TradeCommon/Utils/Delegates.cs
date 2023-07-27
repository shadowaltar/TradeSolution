using TradeCommon.Essentials.Trading;

namespace TradeCommon.Utils;
public static class Delegates
{
    public delegate void OrderPlacedCallback(bool isSuccessful, Order order);
    public delegate void OrderModifiedCallback(bool isSuccessful, Order orderSent, Order orderReceived);
    public delegate void OrderCanceledCallback(bool isSuccessful, Order order);
    public delegate void AllOrderCanceledCallback(bool isSuccessful, IList<Order> orders);

    public delegate void TradeReceivedCallback(Trade trade);
}
