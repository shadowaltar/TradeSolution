﻿using TradeCommon.Essentials.Instruments;
using TradeCommon.Essentials.Portfolios;
using TradeCommon.Essentials.Trading;
using TradeCommon.Runtime;

namespace TradeLogicCore.Services;

public interface IOrderService
{
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
    event Action<Order>? OrderProcessed;

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
    /// <param name="statuses"></param>
    /// <returns></returns>
    Task<List<Order>> GetExternalOrders(Security security, DateTime start, DateTime? end = null, params OrderStatus[] statuses);

    /// <summary>
    /// Get all orders from storage for a specific security and optional time range.
    /// </summary>
    /// <param name="security"></param>
    /// <param name="start"></param>
    /// <param name="end"></param>
    /// <param name="statuses"></param>
    /// <returns></returns>
    Task<List<Order>> GetStorageOrders(Security security, DateTime start, DateTime? end = null, params OrderStatus[] statuses);

    /// <summary>
    /// Get cached orders for a specific security and optional time range.
    /// </summary>
    /// <param name="security"></param>
    /// <param name="start"></param>
    /// <param name="end"></param>
    /// <param name="statuses"></param>
    /// <returns></returns>
    List<Order> GetOrders(Security security, DateTime start, DateTime? end = null, params OrderStatus[] statuses);

    /// <summary>
    /// Get cached open orders; when optional security is specified, only return the open orders related to that security.
    /// </summary>
    /// <param name="security"></param>
    /// <returns></returns>
    List<Order> GetOpenOrders(Security? security = null);

    /// <summary>
    /// Get open orders from external; when optional security is specified, only return the open orders related to that security.
    /// NOTE: it will not update cache.
    /// </summary>
    /// <param name="security"></param>
    /// <returns></returns>
    Task<List<Order>> GetExternalOpenOrders(Security? security = null);

    /// <summary>
    /// Get an order from cache by its external id.
    /// </summary>
    /// <param name="externalOrderId"></param>
    /// <returns></returns>
    Order? GetOrderByExternalId(long externalOrderId);

    /// <summary>
    /// Get order states related to a security from a start date.
    /// TODO: accountId is not supported yet.
    /// </summary>
    /// <param name="security"></param>
    /// <param name="start"></param>
    /// <param name="end"></param>
    /// <returns></returns>
    Task<List<OrderState>> GetOrderStates(Security security, DateTime start, DateTime? end = null);

    /// <summary>
    /// Send an order.
    /// Before sent it will be stored with <see cref="OrderStatus.Sending"/>.
    /// After sent, depending on the result, the state may contain <see cref="ResultCode.SendOrderOk"/>
    /// or <see cref="ResultCode.SendOrderFailed"/>.
    /// The order will also be stored after sent.
    /// </summary>
    /// <param name="order"></param>
    Task<ExternalQueryState> SendOrder(Order order);

    /// <summary>
    /// Cancel an order.
    /// </summary>
    /// <param name="order"></param>
    Task<bool> CancelOrder(Order order);

    /// <summary>
    /// Cancel all open orders.
    /// Need to specify whether sync with external to get the most precise status of open orders.
    /// </summary>
    Task<bool> CancelAllOpenOrders(Security security, OrderActionType action, bool syncExternal);

    /// <summary>
    /// Cancel all open orders immediately.
    /// </summary>
    /// <returns></returns>
    Task<bool> CancelAllOpenOrders();

    /// <summary>
    /// Create an order without any validation.
    /// To execute, use <see cref="SendOrder"/>.
    /// </summary>
    /// <param name="security"></param>
    /// <param name="orderType"></param>
    /// <param name="price"></param>
    /// <param name="quantity"></param>
    /// <param name="side"></param>
    /// <param name="timeInForce"></param>
    /// <param name="comment"></param>
    /// <returns></returns>
    Order CreateManualOrder(Security security,
                            decimal price,
                            decimal quantity,
                            Side side,
                            OrderType orderType = OrderType.Limit,
                            string comment = "manual",
                            TimeInForceType timeInForce = TimeInForceType.GoodTillCancel);

    //Task<bool> SendLongLimitOrder(string securityCode, decimal price, decimal quantity, string comment = "", TimeInForceType timeInForce = TimeInForceType.GoodTillCancel);
    //Task<bool> SendLongMarketOrder(string securityCode, decimal quantity, string comment = "", TimeInForceType timeInForce = TimeInForceType.GoodTillCancel);
    //Task<bool> SendShortLimitOrder(string securityCode, decimal price, decimal quantity, string comment = "", TimeInForceType timeInForce = TimeInForceType.GoodTillCancel);
    //Task<bool> SendShortMarketOrder(string securityCode, decimal quantity, string comment = "", TimeInForceType timeInForce = TimeInForceType.GoodTillCancel);

    /// <summary>
    /// Reset all caches in this service.
    /// </summary>
    void Reset();

    /// <summary>
    /// Update the internal state.
    /// </summary>
    /// <param name="orders"></param>
    /// <param name="security"></param>
    void Update(ICollection<Order> orders, Security? security = null);
    void Update(ICollection<OrderState> orderStates, Security? security = null);

    /// <summary>
    /// Clear cached orders which their positions are closed.
    /// Or specify a closed position and clear its related orders.
    /// </summary>
    /// <param name="position"></param>
    void ClearCachedClosedPositionOrders(Position? position = null);
    bool IsOperational(long orderId);

    ///// <summary>
    ///// Persist an order to data storage.
    ///// </summary>
    ///// <param name="order"></param>
    //void Persist(Order order);
}