using TradeCommon.Essentials.Instruments;
using TradeCommon.Essentials.Trading;

namespace TradeLogicCore.Services;

public interface IOrderService
{
    bool IsFakeOrderSupported { get; }

    /// <summary>
    /// Invoked when an order is successfully created.
    /// </summary>
    event Action<Order>? OrderAcknowledged;

    /// <summary>
    /// Invoked when an order is successfully cancelled.
    /// </summary>
    event Action<Order>? OrderCancelled;

    /// <summary>
    /// Get an order from cache by its id.
    /// </summary>
    /// <param name="orderId"></param>
    /// <returns></returns>
    Order? GetOrder(long orderId);

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
                            OrderTimeInForceType timeInForce = OrderTimeInForceType.GoodTillCancel);
}