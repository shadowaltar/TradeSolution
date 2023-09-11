using TradeCommon.Essentials.Instruments;
using TradeCommon.Essentials.Portfolios;
using TradeCommon.Essentials.Trading;

namespace TradeLogicCore.Services;

public interface IOrderService
{
    bool IsFakeOrderSupported { get; }

    /// <summary>
    /// Invoked when an order is successfully created.
    /// </summary>
    event Action<Order>? AfterOrderSent;

    /// <summary>
    /// Invoked when an order is successfully cancelled.
    /// </summary>
    event Action<Order>? OrderCancelled;

    /// <summary>
    /// Invoked when an order is successfully closed.
    /// </summary>
    event Action? OrderClosed;

    /// <summary>
    /// Invoked when a stop-loss order is successfully triggered.
    /// </summary>
    event Action? OrderStoppedLost;

    /// <summary>
    /// Invoked when a take-profit order is successfully triggered.
    /// </summary>
    event Action? OrderTookProfit;

    /// <summary>
    /// Invoked when an order was sent but a failure message received from external.
    /// </summary>
    event Action? OrderSendingFailed;

    /// <summary>
    /// Invoked when any order changes are received.
    /// </summary>
    event Action<Order>? NextOrder;

    /// <summary>
    /// Get an order from cache by its id.
    /// </summary>
    /// <param name="orderId"></param>
    /// <returns></returns>
    Order? GetOrder(long orderId);

    /// <summary>
    /// Get all orders given by a time range, and optional security name.
    /// </summary>
    /// <param name="start"></param>
    /// <param name="end"></param>
    /// <param name="security"></param>
    /// <param name="requestExternal"></param>
    /// <returns></returns>
    Task<Order[]> GetOrderHistory(DateTime start, DateTime end, Security security, bool requestExternal = false);

    /// <summary>
    /// Get all open orders with an optional security name.
    /// </summary>
    /// <param name="security"></param>
    /// <param name="requestExternal"></param>
    /// <returns></returns>
    Task<List<Order>> GetOpenOrders(Security? security = null, bool requestExternal = false);

    /// <summary>
    /// Get an order from cache by its external id.
    /// </summary>
    /// <param name="externalOrderId"></param>
    /// <returns></returns>
    Order? GetOrderByExternalId(long externalOrderId);

    /// <summary>
    /// Place an order without waiting for the result.
    /// </summary>
    /// <param name="order"></param>
    /// <param name="isFakeOrder"></param>
    void SendOrder(Order order, bool isFakeOrder = true);

    /// <summary>
    /// Cancel an order without waiting for the result.
    /// </summary>
    /// <param name="orderId"></param>
    void CancelOrder(long orderId);

    /// <summary>
    /// Cancel all open orders.
    /// </summary>
    void CancelAllOpenOrders();

    /// <summary>
    /// Close all open positions by market price.
    /// </summary>
    void CloseAllOpenPositions();

    /// <summary>
    /// Create an order without any validation.
    /// To execute, use <see cref="SendOrder"/>.
    /// </summary>
    /// <param name="security"></param>
    /// <param name="account"></param>
    /// <param name="orderType"></param>
    /// <param name="price"></param>
    /// <param name="quantity"></param>
    /// <param name="side"></param>
    /// <param name="timeInForce"></param>
    /// <returns></returns>
    Order CreateManualOrder(Security security,
                            int account,
                            decimal price,
                            decimal quantity,
                            Side side,
                            OrderType orderType = OrderType.Limit,
                            TimeInForceType timeInForce = TimeInForceType.GoodTillCancel);
}