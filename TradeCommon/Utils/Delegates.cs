using TradeCommon.Essentials.Trading;
using TradeCommon.Runtime;

namespace TradeCommon.Utils;
public static class Delegates
{
    public delegate void OrderPlacedCallback(bool isSuccessful, ExternalQueryState state);
    public delegate void OrderModifiedCallback(bool isSuccessful, Order orderSent, Order orderReceived);
    public delegate void OrderCancelledCallback(bool isSuccessful, ExternalQueryState state);
    public delegate void AllOrderCancelledCallback(bool isSuccessful, IList<Order> orders);

    public delegate void TradeReceivedCallback(Trade trade);
    public delegate void TradesReceivedCallback(Trade[] trades);
}
