﻿using TradeCommon.Essentials.Accounts;
using TradeCommon.Essentials.Instruments;
using TradeCommon.Essentials.Trading;
using TradeCommon.Externals;
using TradeCommon.Runtime;
using static TradeCommon.Utils.Delegates;

namespace TradeConnectivity.Futu.Services;
public class Execution : IExternalExecutionManagement
{
    public bool IsFakeOrderSupported => throw new NotImplementedException();

    public event OrderPlacedCallback? OrderPlaced;
    public event OrderModifiedCallback? OrderModified;
    public event OrderCancelledCallback? OrderCancelled;
    public event AllOrderCancelledCallback? AllOrderCancelled;
    public event TradeReceivedCallback? TradeReceived;
    public event TradesReceivedCallback? TradesReceived;

    public Task<ExternalQueryState<List<Order>>> CancelAllOrders(Security security)
    {
        throw new NotImplementedException();
    }

    public Task<ExternalQueryState<Order>> CancelOrder(Order order)
    {
        throw new NotImplementedException();
    }

    public Task<ExternalQueryState<Account>> GetAccount()
    {
        throw new NotImplementedException();
    }

    public Task<ExternalQueryState<List<Trade>?>> GetMarketTrades(Security security)
    {
        throw new NotImplementedException();
    }

    public Task<ExternalQueryState<List<Order>?>> GetOpenOrders(Security? security = null)
    {
        throw new NotImplementedException();
    }

    public Task<ExternalQueryState<Order>> GetOrder(Security security, long orderId = 0, long externalOrderId = 0)
    {
        throw new NotImplementedException();
    }

    public Task<ExternalQueryState<List<Order>?>> GetOrderHistory(DateTime start, DateTime end)
    {
        throw new NotImplementedException();
    }

    public Task<ExternalQueryState<int>> GetOrderSpeedLimit()
    {
        throw new NotImplementedException();
    }

    public Task<bool> Initialize(User user)
    {
        throw new NotImplementedException();
    }

    public Task<ExternalQueryState<Order>> SendOrder(Order order)
    {
        throw new NotImplementedException();
    }

    public Task<ExternalQueryState<Order>> UpdateOrder(Order order)
    {
        throw new NotImplementedException();
    }
}
