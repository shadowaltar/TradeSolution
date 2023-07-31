using TradeCommon.Essentials.Trading;

namespace TradeLogicCore.Services;

public interface IOrderService
{
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
    void SendOrder(Order order);

    /// <summary>
    /// Cancel an order without waiting for the result.
    /// </summary>
    /// <param name="orderId"></param>
    void CancelOrder(long orderId);
}