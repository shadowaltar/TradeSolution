using TradeCommon.Essentials;
using TradeCommon.Essentials.Portfolios;
using TradeCommon.Essentials.Quotes;
using TradeCommon.Essentials.Trading;
using TradeCommon.Runtime;

namespace TradeCommon.Utils;
public static class Delegates
{
    public delegate void OrderPlacedCallback(bool isSuccessful, ExternalQueryState state);
    public delegate void OrderModifiedCallback(bool isSuccessful, Order orderSent, Order orderReceived);
    public delegate void OrderCancelledCallback(bool isSuccessful, ExternalQueryState state);
    public delegate void OrderReceivedCallback(Order order);

    public delegate void AllOrderCancelledCallback(bool isSuccessful, IList<Order> orders);

    public delegate void TradeReceivedCallback(Trade trade);
    public delegate void TradesReceivedCallback(List<Trade> trades, bool isSameSecurity);

    public delegate void AssetsChangedCallback(List<Asset> assets);
    public delegate void TransferredCallback(TransferAction transferAction);

    public delegate void OhlcPriceReceivedCallback(int securityId, OhlcPrice price, IntervalType interval, bool isComplete);
    public delegate void TickPriceReceivedCallback(int securityId, string securityCode, Tick tick);
    public delegate void OrderBookReceivedCallback(ExtendedOrderBook orderBook);
}
