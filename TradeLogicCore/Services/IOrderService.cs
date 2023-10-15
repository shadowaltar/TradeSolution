using TradeCommon.Essentials.Instruments;
using TradeCommon.Essentials.Portfolios;
using TradeCommon.Essentials.Trading;
using TradeCommon.Runtime;

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
    /// Get all orders from external for a specific security and optional time range.
    /// </summary>
    /// <param name="security"></param>
    /// <param name="start"></param>
    /// <param name="end"></param>
    /// <returns></returns>
    Task<List<Order>> GetExternalOrders(Security security, DateTime start, DateTime? end = null);
    
    /// <summary>
    /// Get all orders from storage for a specific security and optional time range.
    /// </summary>
    /// <param name="security"></param>
    /// <param name="start"></param>
    /// <param name="end"></param>
    /// <returns></returns>
    Task<List<Order>> GetStorageOrders(Security security, DateTime start, DateTime? end = null);

    /// <summary>
    /// Get cached orders for a specific security and optional time range.
    /// </summary>
    /// <param name="security"></param>
    /// <param name="start"></param>
    /// <param name="end"></param>
    /// <returns></returns>
    List<Order> GetOrders(Security security, DateTime start, DateTime? end = null);

    /// <summary>
    /// Get cached open orders; when optional security is specified, only return the open orders related to that security.
    /// </summary>
    /// <param name="security"></param>
    /// <returns></returns>
    List<Order> GetOpenOrders(Security? security = null);

    /// <summary>
    /// Get stord open orders; when optional security is specified, only return the open orders related to that security.
    /// NOTE: it will also update cache.
    /// </summary>
    /// <param name="security"></param>
    /// <returns></returns>
    Task<List<Order>> GetStoredOpenOrders(Security? security = null);

    /// <summary>
    /// Get open orders from external; when optional security is specified, only return the open orders related to that security.
    /// NOTE: it will not update cache.
    /// </summary>
    /// <param name="security"></param>
    /// <returns></returns>
    Task<List<Order>> GetExternalOpenOrders(Security? security = null);

    /// <summary>
    /// Get all the orders in this execution session.
    /// </summary> 
    /// <returns></returns>
    List<Order> GetOrders(Security? security = null, bool requestExternal = false);

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
    Task<ExternalQueryState> SendOrder(Order order, bool isFakeOrder = false);

    /// <summary>
    /// Cancel an order without waiting for the result.
    /// </summary>
    /// <param name="orderId"></param>
    void CancelOrder(long orderId);

    /// <summary>
    /// Cancel all open orders.
    /// </summary>
    Task CancelAllOpenOrders(Security security);

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
    /// <param name="comment"></param>
    /// <returns></returns>
    Order CreateManualOrder(Security security,
                            int account,
                            decimal price,
                            decimal quantity,
                            Side side,
                            OrderType orderType = OrderType.Limit,
                            TimeInForceType timeInForce = TimeInForceType.GoodTillCancel,
                            string comment = "manual");

    /// <summary>
    /// Update the internal state.
    /// </summary>
    /// <param name="orders"></param>
    /// <param name="security"></param>
    void Update(ICollection<Order> orders, Security? security = null);

    /// <summary>
    /// Clear cached orders which their positions are closed.
    /// Or specify a closed position and clear its related orders.
    /// </summary>
    /// <param name="position"></param>
    void ClearCachedClosedPositionOrders(Position? position = null);

    ///// <summary>
    ///// Persist an order to data storage.
    ///// </summary>
    ///// <param name="order"></param>
    //void Persist(Order order);
}