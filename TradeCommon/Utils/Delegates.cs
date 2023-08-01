using TradeCommon.Essentials.Trading;

namespace TradeCommon.Utils;
public static class Delegates
{
    public delegate void OrderPlacedCallback(bool isSuccessful, Order order);
    public delegate void OrderModifiedCallback(bool isSuccessful, Order orderSent, Order orderReceived);
    public delegate void OrderCancelledCallback(bool isSuccessful, Order order);
    public delegate void AllOrderCancelledCallback(bool isSuccessful, IList<Order> orders);

    public delegate void TradeReceivedCallback(Trade trade);
    public delegate void TradesReceivedCallback(List<Trade> trade);
}
