﻿using Autofac;
using Common;
using Microsoft.AspNetCore.Mvc;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using TradeCommon.Algorithms;
using TradeCommon.Constants;
using TradeCommon.Essentials;
using TradeCommon.Essentials.Algorithms;
using TradeCommon.Essentials.Instruments;
using TradeCommon.Essentials.Portfolios;
using TradeCommon.Essentials.Trading;
using TradeCommon.Runtime;
using TradeDataCore.Instruments;
using TradeLogicCore;
using TradeLogicCore.Algorithms;
using TradeLogicCore.Algorithms.Screening;
using TradeLogicCore.Services;
using TradePort.Utils;

namespace TradePort.Controllers;

/// <summary>
/// Provides order execution, portfolio and account management.
/// </summary>
[ApiController]
[Route(RestApiConstants.ExecutionRoot)]
public class ExecutionController : Controller
{
    /// <summary>
    /// Manually send an order.
    /// </summary>
    /// <param name="securityService"></param>
    /// <param name="orderService"></param>
    /// <param name="portfolioService"></param>
    /// <param name="context"></param>
    /// <param name="adminPassword">Required.</param>
    /// <param name="secTypeStr">Security type.</param>
    /// <param name="symbol">Symbol of security.</param>
    /// <param name="side">Side of order.</param>
    /// <param name="orderType">Type of order. Only Limit/Market/StopLimit/StopMarket orders are supported.</param>
    /// <param name="price">Price of order. If Market order this is ignored; otherwise it must be > 0.</param>
    /// <param name="quantity">Quantity of order. Must be > 0.</param>
    /// <param name="stopLoss">Stop loss price of order. Must be > 0.</param>
    /// <returns></returns>
    [HttpPost(RestApiConstants.SendOrder)]
    public async Task<ActionResult> SendOrder([FromServices] ISecurityService securityService,
                                              [FromServices] IOrderService orderService,
                                              [FromServices] IPortfolioService portfolioService,
                                              [FromServices] Context context,
                                              [FromForm(Name = "admin-password")] string adminPassword,
                                              [FromQuery(Name = "sec-type")] string? secTypeStr = "fx",
                                              [FromQuery(Name = "symbol")] string symbol = "BTCUSDT",
                                              [FromQuery(Name = "side")] Side side = Side.None,
                                              [FromQuery(Name = "order-type")] OrderType orderType = OrderType.Limit,
                                              [FromQuery(Name = "price")] decimal price = 0,
                                              [FromQuery(Name = "quantity")] decimal quantity = 0,
                                              [FromQuery(Name = "stop-loss")] decimal stopLoss = 0.002m)
    {
        if (ControllerValidator.IsAdminPasswordBad(adminPassword, out var br)) return br;
        if (ControllerValidator.IsBadOrParse(secTypeStr, out SecurityType secType, out br)) return br;
        if (side == Side.None) return BadRequest("Invalid side.");
        if (ControllerValidator.IsDecimalNegative(price, out br)) return br;
        if (ControllerValidator.IsDecimalNegativeOrZero(quantity, out br)) return br;
        if (ControllerValidator.IsDecimalNegativeOrZero(stopLoss, out br)) return br;

        var security = await securityService.GetSecurity(symbol, context.Exchange, secType);
        if (security == null) return BadRequest("Cannot find security.");

        var supportedOrderType = new List<OrderType> {
            OrderType.Limit, OrderType.Market, OrderType.StopLimit,
            OrderType.Stop, OrderType.TakeProfit, OrderType.TakeProfitLimit
        };
        if (!supportedOrderType.Contains(orderType))
            return BadRequest("Currently only supports normal/stop/take-profit limit/market market orders.");

        if (price == 0 && orderType != OrderType.Market)
            return BadRequest("Only market order can have zero price.");

        if (context.Account == null || context.Account.Name.IsBlank())
            return BadRequest("Must login first.");

        var order = orderService.CreateManualOrder(security, price, quantity, side, orderType);
        // TODO validate the remaining asset by rules defined from portfolio service only
        if (!portfolioService.Validate(order))
            return BadRequest("Invalid order price or quantity.");

        // as a manual order, no need to go through algorithm position sizing rules
        await orderService.SendOrder(order);
        return Ok(order);
    }

    ///// <summary>
    ///// Query all orders of a symbol.
    ///// </summary>
    ///// <param name="securityService"></param>
    ///// <param name="adminService"></param>
    ///// <param name="orderService"></param>
    ///// <param name="context"></param>
    ///// <param name="password"></param>
    ///// <param name="accountName"></param>
    ///// <param name="exchange"></param>
    ///// <param name="environment"></param>
    ///// <param name="secTypeStr"></param>
    ///// <param name="symbol"></param>
    ///// <returns></returns>
    //[HttpPost(RestApiConstants.QueryOrders)]
    //public async Task<ActionResult> GetAllOrders([FromServices] ISecurityService securityService,
    //                                             [FromServices] IAdminService adminService,
    //                                             [FromServices] IOrderService orderService,
    //                                             [FromServices] Context context,
    //                                             [FromForm(Name = "admin-password")] string password,
    //                                             [FromQuery(Name = "sec-type")] string? secTypeStr = "fx",
    //                                             [FromQuery(Name = "symbol")] string symbol = "BTCTUSD")
    //{
    //    if (ControllerValidator.IsAdminPasswordBad(password, out var br)) return br;
    //    if (!adminService.IsLoggedIn) return BadRequest("Must login first.");
    //    if (ControllerValidator.IsBadOrParse(secTypeStr, out SecurityType secType, out br)) return br;

    //    var security = await securityService.GetSecurity(symbol, secType);
    //    if (security == null) return BadRequest("Cannot find security.");
    //    if (context.Account == null) return BadRequest("Must login first.");

    //    var orders = await orderService.GetExternalOrders(security, DateTime.MinValue);
    //    return Ok(orders);
    //}

    /// <summary>
    /// Cancel an open order by its id.
    /// Under construction.
    /// </summary>
    /// <returns></returns>
    [HttpPost(RestApiConstants.CancelOrder)]
    public async Task<ActionResult> CancelOrder([FromServices] IOrderService orderService,
                                                [FromForm(Name = "admin-password")] string adminPassword,
                                                [FromForm(Name = "order-id")] long? orderId,
                                                [FromForm(Name = "external-order-id")] string? externalOrderId)
    {
        if (ControllerValidator.IsAdminPasswordBad(adminPassword, out var br)) return br;
        if (Conditions.AllNull(orderId, externalOrderId)) return BadRequest("Either order id or external order id must be specified.");

        object? cancelledOrder = null;
        var a = new AutoResetEvent(true);
        orderService.OrderCancelled += OnOrderCancelled;
        if (orderId != null)
        {
            var order = orderService.GetOrder(orderId.Value);
            if (order == null)
                return BadRequest("Order is not found, id: " + orderId);
            await orderService.CancelOrder(order);
            a.WaitOne();
        }

        void OnOrderCancelled(Order order)
        {
            a.Set();
            cancelledOrder = order;
        }
        orderService.OrderCancelled -= OnOrderCancelled;
        return Ok(cancelledOrder ??= "Failed to cancel.");
    }

    /// <summary>
    /// Cancel an open order by its id.
    /// Under construction.
    /// </summary>
    /// <returns></returns>
    [HttpPost(RestApiConstants.CancelAllOrders)]
    public async Task<ActionResult> CancelAllOrders([FromServices] IOrderService orderService,
                                                    [FromForm(Name = "admin-password")] string adminPassword)
    {
        if (ControllerValidator.IsAdminPasswordBad(adminPassword, out var br)) return br;

        var r = await orderService.CancelAllOpenOrders();
        return !r ? BadRequest("Failed to cancel.") : Ok("Cancelled all open orders.");
    }

    [HttpPost(RestApiConstants.QueryOrders)]
    public async Task<ActionResult> GetOrders([FromServices] IServices services,
                                              [FromForm(Name = "admin-password")] string? adminPassword,
                                              [FromQuery(Name = "start")] string startStr = "20231101",
                                              [FromQuery(Name = "symbol")] string symbol = "BTCUSDT",
                                              [FromQuery(Name = "is-alive-only")] bool isAliveOnly = false,
                                              [FromQuery(Name = "is-fills-only")] bool isFillsOnly = false,
                                              [FromQuery(Name = "is-error-only")] bool isErrorsOnly = false,
                                              [FromQuery(Name = "is-cancel-only")] bool isCancelsOnly = false,
                                              [FromQuery(Name = "where")] DataSourceType dataSourceType = DataSourceType.MemoryCached)
    {
        if (ControllerValidator.IsAdminPasswordBad(adminPassword, out var br)) return br;
        if (ControllerValidator.IsBadOrParse(startStr, out DateTime start, out br)) return br;
        if (!services.Admin.IsLoggedIn) return BadRequest("Must login user and account");

        var security = services.Security.GetSecurity(symbol);
        if (security == null || security.QuoteSecurity == null) return BadRequest("Invalid or missing security.");
        var filteringStatuses = isAliveOnly
            ? OrderStatuses.Lives
            : isFillsOnly
            ? OrderStatuses.Fills
            : isErrorsOnly
            ? OrderStatuses.Errors
            : isCancelsOnly
            ? OrderStatuses.Cancels
            : Array.Empty<OrderStatus>();
        return dataSourceType switch
        {
            DataSourceType.MemoryCached => Ok(services.Order.GetOrders(security, start, null, filteringStatuses)),
            DataSourceType.InternalStorage => Ok(await services.Order.GetStorageOrders(security, start, null, filteringStatuses)),
            DataSourceType.External => Ok(await services.Order.GetExternalOrders(security, start, null, filteringStatuses)),
            _ => BadRequest("Impossible"),
        };
    }

    [HttpPost(RestApiConstants.QueryOrderStates)]
    public async Task<ActionResult> GetOrderStates([FromServices] IServices services,
                                                   [FromForm(Name = "admin-password")] string? adminPassword,
                                                   [FromQuery(Name = "start")] string startStr = "20231101",
                                                   [FromQuery(Name = "symbol")] string symbol = "BTCUSDT")
    {
        if (ControllerValidator.IsAdminPasswordBad(adminPassword, out var br)) return br;
        if (ControllerValidator.IsBadOrParse(startStr, out DateTime start, out br)) return br;
        if (!services.Admin.IsLoggedIn) return BadRequest("Must login user and account");

        var security = services.Security.GetSecurity(symbol);
        return security == null || security.QuoteSecurity == null
            ? BadRequest("Invalid or missing security.")
            : Ok(await services.Order.GetOrderStates(security, start));
    }

    [HttpPost(RestApiConstants.QueryTrades)]
    public async Task<ActionResult> GetTrades([FromServices] IServices services,
                                              [FromForm(Name = "admin-password")] string? adminPassword,
                                              [FromQuery(Name = "start")] string startStr = "20231101",
                                              [FromQuery(Name = "symbol")] string symbol = "BTCUSDT",
                                              [FromQuery(Name = "where")] DataSourceType dataSourceType = DataSourceType.MemoryCached)
    {
        if (ControllerValidator.IsAdminPasswordBad(adminPassword, out var br)) return br;
        if (ControllerValidator.IsBadOrParse(startStr, out DateTime start, out br)) return br;
        if (!services.Admin.IsLoggedIn) return BadRequest("Must login user and account");

        var security = services.Security.GetSecurity(symbol);
        if (security == null || security.QuoteSecurity == null) return BadRequest("Invalid or missing security.");

        switch (dataSourceType)
        {
            case DataSourceType.MemoryCached:
                var cached = services.Trade.GetTrades(security);
                return Ok(cached.Select(o => o.Time >= start));
            case DataSourceType.InternalStorage:
                return Ok(await services.Trade.GetStorageTrades(security, start, null, false));
            case DataSourceType.External:
                return Ok(await services.Trade.GetExternalTrades(security, start));
            default:
                return BadRequest("Impossible");
        }
    }

    /// <summary>
    /// Get positions. Optionally can get the initial state of positions before algo engine is started.
    /// </summary>
    /// <param name="services"></param>
    /// <param name="adminPassword"></param>
    /// <param name="startStr"></param>
    /// <param name="symbol"></param>
    /// <param name="dataSourceType"></param>
    /// <param name="isInitialPortfolio"></param>
    /// <returns></returns>
    [HttpPost(RestApiConstants.QueryPositions)]
    public async Task<ActionResult> GetPositions([FromServices] IServices services,
                                                 [FromForm(Name = "admin-password")] string? adminPassword,
                                                 [FromQuery(Name = "start")] string startStr = "20231101",
                                                 [FromQuery(Name = "symbol")] string symbol = "BTCUSDT",
                                                 [FromQuery(Name = "where")] DataSourceType dataSourceType = DataSourceType.MemoryCached,
                                                 [FromQuery(Name = "get-initial-state")] bool isInitialPortfolio = false)
    {
        if (ControllerValidator.IsAdminPasswordBad(adminPassword, out var br)) return br;
        if (ControllerValidator.IsBadOrParse(startStr, out DateTime start, out br)) return br;
        if (!services.Admin.IsLoggedIn) return BadRequest("Must login user and account");

        var security = services.Security.GetSecurity(symbol);
        if (security == null || security.QuoteSecurity == null) return BadRequest("Invalid or missing security.");

        if (!isInitialPortfolio)
            switch (dataSourceType)
            {
                case DataSourceType.MemoryCached:
                    var cached = services.Portfolio.GetPositions();
                    return Ok(cached.Select(o => o.UpdateTime >= start));
                case DataSourceType.InternalStorage:
                    return Ok(await services.Portfolio.GetStoragePositions(start));
                case DataSourceType.External:
                    return BadRequest("External system (broker/exchange) does not support position info; maybe you are looking for Asset query API?");
                default:
                    return BadRequest("Impossible");
            }
        else
            switch (dataSourceType)
            {
                case DataSourceType.MemoryCached:
                    var cached = services.Portfolio.InitialPortfolio.GetPositions();
                    return Ok(cached.Select(o => o.UpdateTime >= start));
                case DataSourceType.InternalStorage:
                    return BadRequest("Initial portfolio positions only exists in memory, as portfolio changes are always synchronized to internal storage.");
                case DataSourceType.External:
                    return BadRequest("Initial portfolio positions only exists in memory, external asset position is always the most updated.");
                default:
                    return BadRequest("Impossible");
            }
    }

    /// <summary>
    /// Gets asset positions. Optionally can get the initial state of assets before algo engine is started.
    /// </summary>
    /// <param name="services"></param>
    /// <param name="adminPassword"></param>
    /// <param name="symbolStr"></param>
    /// <param name="dataSourceType"></param>
    /// <param name="isInitialPortfolio"></param>
    /// <returns></returns>
    [HttpPost(RestApiConstants.QueryAssets)]
    public async Task<ActionResult> GetAssets([FromServices] IServices services,
                                              [FromForm(Name = "admin-password")] string? adminPassword,
                                              [FromQuery(Name = "symbols")] string symbolStr = "BTC,USDT,USDT",
                                              [FromQuery(Name = "where")] DataSourceType dataSourceType = DataSourceType.MemoryCached,
                                              [FromQuery(Name = "get-initial-state")] bool isInitialPortfolio = false)
    {
        if (ControllerValidator.IsAdminPasswordBad(adminPassword, out var br)) return br;
        if (!services.Admin.IsLoggedIn) return BadRequest("Must login user and account");

        var codes = symbolStr.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var securities = new List<Security>();
        foreach (var code in codes)
        {
            var security = services.Security.GetSecurity(code);
            if (security == null || !security.IsAsset) return BadRequest("Invalid code / missing asset security / expect asset code but security code is provided.");
            securities.Add(security);
        }

        object results = !isInitialPortfolio
            ? dataSourceType switch
            {
                DataSourceType.MemoryCached => services.Portfolio.GetAssets(),
                DataSourceType.InternalStorage => await services.Portfolio.GetStorageAssets(),
                DataSourceType.External => await services.Portfolio.GetExternalAssets(),
                _ => BadRequest("Impossible"),
            }
            : dataSourceType switch
            {
                DataSourceType.MemoryCached => services.Portfolio.InitialPortfolio.GetAssets(),
                DataSourceType.InternalStorage => BadRequest("Initial portfolio assets only exists in memory, as portfolio changes are always synchronized to internal storage."),
                DataSourceType.External => BadRequest("Initial portfolio assets only exists in memory, external asset position is always the most updated."),
                _ => BadRequest("Impossible"),
            };
        if (results is ObjectResult r) return r;
        var assets = ((List<Asset>)results).Where(a => securities.Contains(a.Security)).ToList();
        return Ok(assets);
    }

    /// <summary>
    /// Gets each changes of asset positions.
    /// </summary>
    /// <param name="services"></param>
    /// <param name="startStr"></param>
    /// <param name="adminPassword"></param>
    /// <param name="symbolStr"></param>
    /// <returns></returns>
    [HttpPost(RestApiConstants.QueryAssetStates)]
    public async Task<ActionResult> GetAssetStates([FromServices] IServices services,
                                                   [FromForm(Name = "admin-password")] string? adminPassword,
                                                   [FromQuery(Name = "start")] string startStr = "20231101",
                                                   [FromQuery(Name = "symbols")] string symbolStr = "BTC,USDT")
    {
        if (ControllerValidator.IsAdminPasswordBad(adminPassword, out var br)) return br;
        if (ControllerValidator.IsBadOrParse(startStr, out DateTime start, out br)) return br;
        if (!services.Admin.IsLoggedIn) return BadRequest("Must login user and account");

        var codes = symbolStr.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var securities = new List<Security>();
        foreach (var code in codes)
        {
            var security = services.Security.GetSecurity(code);
            if (security == null || !security.IsAsset) return BadRequest("Invalid code / missing asset security.");
            securities.Add(security);
        }
        var results = new Dictionary<string, List<AssetState>>();
        foreach (var security in securities)
        {
            results[security.Code] = await services.Portfolio.GetAssetStates(security, start);
        }
        return Ok(results);
    }

    /// <summary>
    /// Start an algorithm.
    /// </summary>
    /// <param name="core"></param>
    /// <param name="services"></param>
    /// <param name="macParams"></param>
    /// <param name="algoParams"></param>
    /// <param name="adminPassword"></param>
    /// <param name="symbol">Single symbol for trading.</param>
    /// <param name="intervalStr">Trading time interval</param>
    /// <param name="fastMa">Fast MA parameter.</param>
    /// <param name="slowMa">Slow MA parameter</param>
    /// <param name="stopLoss">Stop loss in ratio.</param>
    /// <param name="takeProfit">Take profit in ratio.</param>
    /// <param name="positionSizingMethod">Position sizing method.
    /// PreserveFixed: give a fixed amount of quote currency at the beginning and then only trade this part,
    /// no matter it grows or shrinks.
    /// Fixed: give a fixed amount of quote currency and all the trades' quantity is fixed to this amount.
    /// </param>
    /// <param name="initialAvailableQuantity">For PreserveFixed/Fixed position sizing, this is the initial quote quantity.</param>
    /// <param name="preferredQuoteCurrencies">ListAlgoBatches of preferred quote currency codes (can be only one), delimited by ",",
    ///     eg. "USDT,USDT". Used as fallback quote ccy when only base ccy or asset-security is specified</param>
    /// <param name="globalCurrencyFilter">ListAlgoBatches of security codes which this algo can only use, delimited by ",".
    ///     If empty, will be derived from <paramref name="symbol"/> and <paramref name="preferredQuoteCurrencies"/> input parameters.</param>
    /// <param name="cancelOpenOrdersOnStart">Cancel any open orders in the market on engine start.</param>
    /// <param name="assumeNoOpenPositionOnStart">Assume no open position exists on engine start.</param>
    /// <param name="closeOpenPositionsOnStart">Close any open positions on engine start (if assume-no-open-position-on-start is false).</param>
    /// <param name="closeOpenPositionsOnStop">Close any open positions on engine stop.</param>
    /// <param name="cleanUpNonCashOnStart">Clean up (usually sell out) any holding assets on engine start, excluding all quote currencies.</param>
    /// <returns></returns>
    [HttpPost(RestApiConstants.StartAlgorithmMac)]
    public async Task<ActionResult?> RunMac([FromServices] Core core,
                                            [FromServices] IServices services,
                                            [FromForm] MacStartModel macParams,
                                            [FromForm] AlgorithmStartModel algoParams)//,
                                                                                      //[FromForm(Name = "admin-password")] string adminPassword,
                                                                                      //[FromForm(Name = "symbol")] string symbol = "BTCUSDT",
                                                                                      //[FromForm(Name = "interval")] string intervalStr = "1m",
                                                                                      //[FromForm(Name = "fast-ma")] int fastMa = 3,
                                                                                      //[FromForm(Name = "slow-ma")] int slowMa = 7,
                                                                                      //[FromForm(Name = "stop-loss")] decimal stopLoss = 0.0005m,
                                                                                      //[FromForm(Name = "take-profit")] decimal takeProfit = 0.0005m,
                                                                                      //[FromForm(Name = "position-sizing-method")] PositionSizingMethod positionSizingMethod = PositionSizingMethod.PreserveFixed,
                                                                                      //[FromForm(Name = "initial-available-quote-quantity")] decimal initialAvailableQuantity = 100,
                                                                                      //[FromForm(Name = "preferred-quote-currencies")] string preferredQuoteCurrencies = "FDUSD",
                                                                                      //[FromForm(Name = "global-currency-filter")] string globalCurrencyFilter = "BTC,USDT,BNB,FDUSD",
                                                                                      //[FromForm(Name = "cancel-open-orders-on-start")] bool cancelOpenOrdersOnStart = true,
                                                                                      //[FromForm(Name = "assume-no-open-position")] bool assumeNoOpenPositionOnStart = true,
                                                                                      //[FromForm(Name = "close-open-position-on-start")] bool closeOpenPositionsOnStart = true,
                                                                                      //[FromForm(Name = "close-open-position-on-stop")] bool closeOpenPositionsOnStop = true,
                                                                                      //[FromForm(Name = "clean-up-non-cash-on-start")] bool cleanUpNonCashOnStart = false)
    {
        if (ControllerValidator.IsAdminPasswordBad(algoParams.AdminPassword, out var br)) return br;
        if (ControllerValidator.IsBadOrParse(algoParams.IntervalStr, out IntervalType interval, out br)) return br;
        if (ControllerValidator.IsIntNegativeOrZero(macParams.FastMa, out br)) return br;
        if (ControllerValidator.IsIntNegativeOrZero(macParams.SlowMa, out br)) return br;
        if (ControllerValidator.IsDecimalNegative(algoParams.StopLoss, out br)) return br;
        if (ControllerValidator.IsDecimalNegative(algoParams.TakeProfit, out br)) return br;
        if (!services.Admin.IsLoggedIn) return BadRequest("Must login user and account");

        var security = services.Security.GetSecurity(algoParams.Symbol);
        if (security == null || security.QuoteSecurity == null) return BadRequest("Invalid or missing security.");
        var quoteCode = security.QuoteSecurity.Code;

        AlgoEffectiveTimeRange? algoTimeRange = null;
        switch (core.Environment)
        {
            case EnvironmentType.Test:
            case EnvironmentType.Uat:
                algoTimeRange = AlgoEffectiveTimeRange.ForPaperTrading(interval);
                break;
            case EnvironmentType.Prod:
                algoTimeRange = AlgoEffectiveTimeRange.ForProduction(interval);
                break;
            default:
                return BadRequest("Invalid environment type to start algo.");
        }

        var preferredCashCodes = algoParams.PreferredQuoteCurrencies.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
        if (preferredCashCodes.Count == 0)
        {
            preferredCashCodes.AddRange("USDT", "USD");
        }

        var whitelistCodes = algoParams.AssetCodeWhitelist.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
        whitelistCodes.AddRange(preferredCashCodes);
        if (security.FxInfo?.BaseAsset != null)
            whitelistCodes.Add(security.FxInfo.BaseAsset.Code);
        if (security.FxInfo?.QuoteAsset != null)
            whitelistCodes.Add(security.FxInfo.QuoteAsset.Code);
        whitelistCodes = whitelistCodes.Distinct().OrderBy(c => c).ToList();

        var ep = new EngineParameters(preferredCashCodes,
                                      whitelistCodes,
                                      algoParams.AssumeNoOpenPositionOnStart,
                                      algoParams.CancelOpenOrdersOnStart,
                                      algoParams.CloseOpenPositionsOnStop,
                                      algoParams.CloseOpenPositionsOnStart,
                                      algoParams.CleanUpNonCashOnStart,
                                      algoParams.RecordOrderBookOnExecution);
        var ap = new AlgorithmParameters(IsBackTesting: false,
                                         Interval: interval,
                                         SecurityPool: new List<Security> { security },
                                         SecurityCodes: new List<string> { security.Code }, // reporting purpose
                                         TimeRange: algoTimeRange,
                                         RequiresTickData: true,
                                         StopOrderTriggerBy: algoParams.StopOrderStyle,
                                         algoParams.StopLoss,
                                         algoParams.TakeProfit);
        var algorithm = new MovingAverageCrossing(services.Context, ap, macParams.FastMa, macParams.SlowMa, algoParams.StopLoss, algoParams.TakeProfit);
        var screening = new SingleSecurityLogic(services.Context, security);
        var sizing = new SimplePositionSizingLogic(algoParams.PositionSizingMethod);
        algorithm.Screening = screening;
        algorithm.Sizing = sizing;
        switch (algoParams.PositionSizingMethod)
        {
            case PositionSizingMethod.PreserveFixed:
                sizing.CalculatePreserveFixed(services.Security, services.Portfolio, quoteCode, algoParams.OpenPositionQuantityHint);
                break;
            case PositionSizingMethod.Fixed:
                sizing.CalculateFixed(services.Security, services.Portfolio, quoteCode, algoParams.OpenPositionQuantityHint);
                break;
        }
        var algoBatch = await core.Run(ep, ap, algorithm);
        return Ok(algoBatch);
    }

    [HttpPost(RestApiConstants.QueryRunningAlgorithms)]
    public ActionResult GetAllRunningAlgorithms([FromServices] Core core,
                                                [FromServices] IAdminService adminService,
                                                [FromForm(Name = "admin-password")] string? adminPassword)
    {
        if (ControllerValidator.IsAdminPasswordBad(adminPassword, out var br)) return br;
        if (!adminService.IsLoggedIn) return BadRequest("Must login user and account first.");

        var ids = core.ListAlgoBatches();
        return Ok(ids);
    }

    /// <summary>
    /// Lists all algo batches.
    /// </summary>
    /// <param name="context"></param>
    /// <returns></returns>
    [HttpPost(RestApiConstants.QueryAlgoBatches)]
    public async Task<ActionResult> GetAlgoBatches([FromServices] Context context)
    {
        if (!context.Services.Admin.IsLoggedIn) return BadRequest("Must login user and account first.");
        var batches = await context.Storage.Read<AlgoBatch>();
        return Ok(batches);
    }

    /// <summary>
    /// Gets all algo entries associated to a specific algo batch.
    /// </summary>
    /// <param name="context"></param>
    /// <param name="batchId"></param>
    /// <returns></returns>
    [HttpPost(RestApiConstants.QueryAlgoEntries)]
    public async Task<ActionResult> GetAlgoEntries([FromServices] Context context, [FromQuery(Name = "batch-id")] long batchId)
    {
        if (!context.Services.Admin.IsLoggedIn) return BadRequest("Must login user and account first.");
        var batches = await context.Storage.Read<AlgoEntry>(null, whereClause: $"{nameof(AlgoEntry.AlgoBatchId)} = $BatchId", ("$BatchId", batchId));
        return Ok(batches);
    }

    [HttpPost(RestApiConstants.StopAlgorithm)]
    public async Task<ActionResult> StopAlgorithm([FromServices] Core core,
                                                  [FromServices] IAdminService adminService,
                                                  [FromForm(Name = "admin-password")] string? adminPassword,
                                                  [FromQuery(Name = "algo-batch-id")] long algoBatchId)
    {
        if (ControllerValidator.IsAdminPasswordBad(adminPassword, out var br)) return br;
        if (!adminService.IsLoggedIn) return BadRequest("Must login user and account first.");

        var resultCode = await core.StopAlgorithm(algoBatchId);
        return Ok($"AlgoBatchId: {algoBatchId}, Result: {resultCode}");
    }

    [HttpPost(RestApiConstants.StopAllAlgorithms)]
    public async Task<ActionResult> StopAllAlgorithms([FromServices] IComponentContext container,
                                                      [FromServices] IAdminService adminService,
                                                      [FromForm(Name = "admin-password")] string? adminPassword)
    {
        if (ControllerValidator.IsAdminPasswordBad(adminPassword, out var br)) return br;
        if (!adminService.IsLoggedIn) return BadRequest("Must login user and account first.");

        var core = container.Resolve<Core>();
        var (expected, successful) = await core.StopAllAlgorithms();
        return Ok(new Dictionary<string, int> { { "expected", expected }, { "successful", successful } });
    }

    public class UserCredentialModel
    {
        [FromForm(Name = "userName")]
        public string? UserName { get; set; }

        [FromForm(Name = "adminPassword")]
        public string? AdminPassword { get; set; }

        [FromForm(Name = "userPassword")]
        public string? UserPassword { get; set; }
    }

    public class MacStartModel
    {
        /// <summary>
        /// Fast MA parameter.
        /// </summary>
        [FromForm(Name = "fast-ma")]
        [Required, DefaultValue(3)]
        public int FastMa { get; set; } = 3;

        /// <summary>
        /// Slow MA parameter.
        /// </summary>
        [FromForm(Name = "slow-ma")]
        [Required, DefaultValue(7)]
        public int SlowMa { get; set; } = 7;
    }

    public class AlgorithmStartModel
    {
        [FromForm(Name = "admin-password")]
        [Required]
        public string AdminPassword { get; set; } = "";

        [FromForm(Name = "symbol")]
        [Required, DefaultValue("BTCUSDT")]
        public string Symbol { get; set; } = "BTCUSDT";

        [FromForm(Name = "interval")]
        [Required, DefaultValue("1m")]
        public string IntervalStr { get; set; } = "1m";

        /// <summary>
        /// Stop loss ratio.
        /// </summary>
        [FromForm(Name = "stop-loss")]
        [Required, DefaultValue(0)]
        public decimal StopLoss { get; set; } = 0.0005m;

        /// <summary>
        /// Take profit ratio.
        /// </summary>
        [FromForm(Name = "take-profit")]
        [Required, DefaultValue(0)]
        public decimal TakeProfit { get; set; } = 0.0005m;

        /// <summary>
        /// Style of stop orders (SL and TP).
        /// * <see cref="StopOrderStyleType.Manual"/>: manual style; for MAC it means no SL/TP logic at all.
        /// * <see cref="StopOrderStyleType.RealOrder"/>: execute a pair of SL and TP order if parent order is accepted and SL/TP ratios are defined.
        /// * <see cref="StopOrderStyleType.TickSignal"/>: execute a real sell order to mimic SL/TP if original open position order is buy (vice versa), when tick price hits threshold price calculated by SL/TP ratios.
        /// </summary>
        [FromForm(Name = "stop-order-style")]
        [Required, DefaultValue(StopOrderStyleType.TickSignal)]
        public StopOrderStyleType StopOrderStyle { get; set; } = StopOrderStyleType.TickSignal;

        /// <summary>
        /// Style of sizing, to determine the order which opens a new position.
        /// * <see cref="PositionSizingMethod.PreserveFixed"/>: only trades what is defined by <see cref="OpenPositionQuantityHint"/>; it is as if your account only has 100 (in quote ccy) and you always trade all of it.
        /// * <see cref="PositionSizingMethod.Fixed"/>: always trades what is defined by <see cref="OpenPositionQuantityHint"/>; if it is 100, all open position orders' quantity is 100.
        /// * <see cref="PositionSizingMethod.All"/>: always trades everything in your asset account.
        /// </summary>
        [FromForm(Name = "position-sizing-method")]
        [Required, DefaultValue(PositionSizingMethod.PreserveFixed)]
        public PositionSizingMethod PositionSizingMethod { get; set; } = PositionSizingMethod.PreserveFixed;

        /// <summary>
        /// Defines trading quantity of orders of open position. Must be combined with <see cref="PositionSizingMethod"/> to get
        /// the true quantity.
        /// </summary>
        [FromForm(Name = "initial-available-quote-quantity")]
        [Required, DefaultValue(100)]
        public decimal OpenPositionQuantityHint { get; set; } = 100;

        /// <summary>
        /// Only these asset codes will be tradable.
        /// </summary>
        [FromForm(Name = "asset-code-whitelist")]
        [Required, DefaultValue("BTC,USDT,BNB,FDUSD")]
        public string AssetCodeWhitelist { get; set; } = "BTC,USDT,BNB,FDUSD";

        /// <summary>
        /// In any situation if an FX quote currency is missing but an order needs to be created,
        /// use these quote currencies. First currency has higher precedence.
        /// </summary>
        [FromForm(Name = "preferred-quote-currencies")]
        [Required, DefaultValue("FDUSD")]
        public string PreferredQuoteCurrencies { get; set; } = "FDUSD";

        /// <summary>
        /// Cancel all open orders on algo start.
        /// </summary>
        [FromForm(Name = "cancel-open-orders-on-start")]
        [DefaultValue(true)]
        public bool CancelOpenOrdersOnStart { get; set; } = true;

        /// <summary>
        /// Assumes no open positions when algo starts.
        /// This affects <see cref="CloseOpenPositionsOnStart"/>.
        /// </summary>
        [FromForm(Name = "assume-no-open-position")]
        [DefaultValue(true)]
        public bool AssumeNoOpenPositionOnStart { get; set; } = true;

        /// <summary>
        /// Closes any open positions.
        /// If <see cref="AssumeNoOpenPositionOnStart"/> is true, this flag becomes useless.
        /// </summary>
        [FromForm(Name = "close-open-position-on-start")]
        [DefaultValue(true)]
        public bool CloseOpenPositionsOnStart { get; set; } = true;

        /// <summary>
        /// TODO!
        /// Closes any open positions when algo stops.
        /// </summary>
        [FromForm(Name = "close-open-position-on-stop")]
        [DefaultValue(true)]
        public bool CloseOpenPositionsOnStop { get; set; } = true;

        /// <summary>
        /// Assuming all other currencies are base currencies.
        /// Sell everything to exchange for <see cref="PreferredQuoteCurrencies"/>.
        /// </summary>
        [FromForm(Name = "clean-up-non-cash-on-start")]
        [DefaultValue(false)]
        public bool CleanUpNonCashOnStart { get; set; } = false;


        [FromForm(Name = "recorder-order-book-on-execution")]
        [DefaultValue(false)]
        public bool RecordOrderBookOnExecution { get; set; } = true;
    }
}
