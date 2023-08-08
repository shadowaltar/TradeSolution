﻿using Common;
using log4net;
using TradeCommon.Constants;
using TradeCommon.Database;
using TradeCommon.Essentials.Trading;
using TradeDataCore.Instruments;
using TradeLogicCore.Services;

namespace TradeLogicCore;
public class BinanceExecutionEngine
{
    private static readonly ILog _log = Logger.New();

    private readonly IPortfolioService _portfolioService;
    private readonly IOrderService _orderService;
    private readonly ITradeService _tradeService;
    private readonly ISecurityService _securityService;

    public ExchangeType ExchangeType => ExchangeType.Binance;

    public BinanceExecutionEngine(IPortfolioService portfolioService,
                                  IOrderService orderService,
                                  ITradeService tradeService,
                                  ISecurityService securityService)
    {
        _portfolioService = portfolioService;
        _orderService = orderService;
        _tradeService = tradeService;
        _securityService = securityService;
    }

    public async Task Start(string accountName)
    {
        ConsolidateAccountAndBalance(accountName);

        ConsolidateOpenOrders();
    }

    private async Task ConsolidateOpenOrders()
    {
        // retrieve previously opened order state, verify with 
        if (TryRetrieveOpenedOrders(out var orders) && !orders.IsNullOrEmpty())
        {
            // there are opened orders in the exchange, consolidate with those stored in db
        }
        var storedOpenOrders = await Storage.ReadOpenOrders(ExchangeType);
        var notStoredOpenOrders = new List<Order>();

        // a stored one does not exist on external side
        foreach (var storedOpenOrder in storedOpenOrders)
        {
            if (!orders.Exists(o => o.ExternalOrderId == storedOpenOrder.ExternalOrderId))
            {

            }
        }
        // an external one does not exist in storage
        foreach (var order in orders)
        {
            if (!storedOpenOrders.Exists(o => o.ExternalOrderId == order.ExternalOrderId))
            {

            }
        }
    }

    private bool TryRetrieveOpenedOrders(out List<Order> orders)
    {
        throw new NotImplementedException();
    }

    private void ConsolidateAccountAndBalance(string accountName)
    {
        // retrieve account state
        RetrieveAccount(accountName);

        void RetrieveAccount(string accountName)
        {

        }
    }
}