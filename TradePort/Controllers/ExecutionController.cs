using Autofac;
using Autofac.Core;
using Common;
using Microsoft.AspNetCore.Mvc;
using System;
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
using TradeCommon.Utils;
using TradeDataCore.Instruments;
using TradeLogicCore;
using TradeLogicCore.Algorithms;
using TradeLogicCore.Algorithms.Screening;
using TradeLogicCore.Maintenance;
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
                                              [FromQuery(Name = "sec-type")] string? secTypeStr = "fx",
                                              [FromQuery(Name = "symbol")] string symbol = "BTCUSDT",
                                              [FromQuery(Name = "side")] Side side = Side.None,
                                              [FromQuery(Name = "order-type")] OrderType orderType = OrderType.Limit,
                                              [FromQuery(Name = "price")] decimal price = 0,
                                              [FromQuery(Name = "quantity")] decimal quantity = 0,
                                              [FromQuery(Name = "stop-loss")] decimal stopLoss = 0.002m)
    {
        if (ControllerValidator.IsBadOrParse(secTypeStr, out SecurityType secType, out var br)) return br;
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
                                                [FromForm(Name = "order-id")] long? orderId,
                                                [FromForm(Name = "external-order-id")] string? externalOrderId)
    {
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
    public async Task<ActionResult> CancelAllOrders([FromServices] Context context,
                                                    [FromServices] IOrderService orderService,
                                                    [FromForm(Name = "admin-password")] string adminPassword)
    {
        if (ControllerValidator.IsAdminPasswordBad(adminPassword, context.Environment, out var br)) return br;

        var r = await orderService.CancelAllOpenOrders();
        return !r ? BadRequest("Failed to cancel.") : Ok("Cancelled all open orders.");
    }

    [HttpPost(RestApiConstants.CloseAllPositions)]
    public async Task<ActionResult> CloseAllPositions([FromServices] Context context,
                                                      [FromServices] IServices services,
                                                      [FromForm(Name = "admin-password")] string adminPassword)
    {
        if (ControllerValidator.IsAdminPasswordBad(adminPassword, context.Environment, out var br)) return br;


        var r = await services.Portfolio.CloseAllPositions(Comments.CloseAll);

        return !r ? BadRequest("Failed to close.") : Ok("Closed all open orders.");
    }

    [HttpPost(RestApiConstants.QueryOrders)]
    public async Task<ActionResult> GetOrders([FromServices] IServices services,
                                              [FromQuery(Name = "start")] string startStr = "20231101",
                                              [FromQuery(Name = "symbol")] string symbol = "BTCUSDT",
                                              [FromQuery(Name = "is-alive-only")] bool isAliveOnly = false,
                                              [FromQuery(Name = "is-fills-only")] bool isFillsOnly = false,
                                              [FromQuery(Name = "is-error-only")] bool isErrorsOnly = false,
                                              [FromQuery(Name = "is-cancel-only")] bool isCancelsOnly = false,
                                              [FromQuery(Name = "where")] DataSourceType dataSourceType = DataSourceType.MemoryCached)
    {
        if (ControllerValidator.IsBadOrParse(startStr, out DateTime start, out var br)) return br;
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
                                                   [FromQuery(Name = "start")] string startStr = "20231101",
                                                   [FromQuery(Name = "symbol")] string symbol = "BTCUSDT")
    {
        if (ControllerValidator.IsBadOrParse(startStr, out DateTime start, out var br)) return br;
        if (!services.Admin.IsLoggedIn) return BadRequest("Must login user and account");

        var security = services.Security.GetSecurity(symbol);
        return security == null || security.QuoteSecurity == null
            ? BadRequest("Invalid or missing security.")
            : Ok(await services.Order.GetOrderStates(security, start));
    }

    [HttpPost(RestApiConstants.QueryTrades)]
    public async Task<ActionResult> GetTrades([FromServices] IServices services,
                                              [FromQuery(Name = "start")] string startStr = "20231101",
                                              [FromQuery(Name = "symbol")] string symbol = "BTCUSDT",
                                              [FromQuery(Name = "where")] DataSourceType dataSourceType = DataSourceType.MemoryCached)
    {
        if (ControllerValidator.IsBadOrParse(startStr, out DateTime start, out var br)) return br;
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
    /// <param name="dataSourceType"></param>
    /// <param name="isInitialPortfolio"></param>
    /// <returns></returns>
    [HttpPost(RestApiConstants.QueryPositions)]
    public async Task<ActionResult> GetAllPositions([FromServices] IServices services,
                                                    [FromQuery(Name = "where")] DataSourceType dataSourceType = DataSourceType.MemoryCached,
                                                    [FromQuery(Name = "get-initial-state")] bool isInitialPortfolio = false)
    {
        if (!services.Admin.IsLoggedIn) return BadRequest("Must login user and account");

        if (!isInitialPortfolio)
            switch (dataSourceType)
            {
                case DataSourceType.MemoryCached:
                    var cached = services.Portfolio.Portfolio.GetAssetPositions();
                    return Ok(cached);
                case DataSourceType.InternalStorage:
                    return Ok(await services.Portfolio.GetStorageAssets());
                case DataSourceType.External:
                    return BadRequest("External system (broker/exchange) does not support position info; maybe you are looking for Asset query API?");
                default:
                    return BadRequest("Impossible");
            }
        else
            switch (dataSourceType)
            {
                case DataSourceType.MemoryCached:
                    var cached = services.Portfolio.InitialPortfolio.GetAssetPositions();
                    return Ok(cached);
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
    /// <param name="symbolStr"></param>
    /// <param name="dataSourceType"></param>
    /// <param name="isInitialPortfolio"></param>
    /// <returns></returns>
    [HttpPost(RestApiConstants.QueryAssets)]
    public async Task<ActionResult> GetAssets([FromServices] IServices services,
                                              [FromQuery(Name = "symbols")] string symbolStr = "BTC,USDT",
                                              [FromQuery(Name = "where")] DataSourceType dataSourceType = DataSourceType.MemoryCached,
                                              [FromQuery(Name = "get-initial-state")] bool isInitialPortfolio = false)
    {
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
                DataSourceType.MemoryCached => services.Portfolio.Portfolio.GetAll(),
                DataSourceType.InternalStorage => await services.Portfolio.GetStorageAssets(),
                DataSourceType.External => await services.Portfolio.GetExternalAssets(),
                _ => BadRequest("Impossible"),
            }
            : dataSourceType switch
            {
                DataSourceType.MemoryCached => services.Portfolio.InitialPortfolio.GetAll(),
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
    /// <param name="symbolStr"></param>
    /// <returns></returns>
    [HttpPost(RestApiConstants.QueryAssetStates)]
    public async Task<ActionResult> GetAssetStates([FromServices] IServices services,
                                                   [FromQuery(Name = "start")] string startStr = "20231101",
                                                   [FromQuery(Name = "symbols")] string symbolStr = "BTC,USDT")
    {
        if (ControllerValidator.IsBadOrParse(startStr, out DateTime start, out var br)) return br;
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
    /// <returns></returns>
    [HttpPost(RestApiConstants.StartAlgorithmMac)]
    public async Task<ActionResult?> RunMac([FromServices] Core core,
                                            [FromServices] IServices services,
                                            [FromForm] MacStartModel macParams,
                                            [FromForm] AlgorithmStartModel algoParams)
    {
        if (ControllerValidator.IsBadOrParse(algoParams.IntervalStr, out IntervalType interval, out var br)) return br;
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
            preferredCashCodes.AddRange("USDT");
        }

        var whitelistCodes = algoParams.AssetCodeWhitelist.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
        whitelistCodes.AddRange(preferredCashCodes);
        if (security.FxInfo?.BaseSecurity != null)
            whitelistCodes.Add(security.FxInfo.BaseSecurity.Code);
        if (security.FxInfo?.QuoteSecurity != null)
            whitelistCodes.Add(security.FxInfo.QuoteSecurity.Code);
        whitelistCodes = whitelistCodes.Distinct().OrderBy(c => c).ToList();

        var ep = new EngineParameters(preferredCashCodes,
                                      whitelistCodes,
                                      CancelOpenOrdersOnStart: algoParams.CancelOpenOrdersOnStart,
                                      CloseOpenPositionsOnStop: algoParams.CloseOpenPositionsOnStop,
                                      CloseOpenPositionsOnStart: algoParams.CloseOpenPositionsOnStart,
                                      RecordOrderBookOnExecution: algoParams.RecordOrderBookOnExecution);
        var ap = new AlgorithmParameters(IsBackTesting: false,
                                         Interval: interval,
                                         SecurityPool: [security],
                                         SecurityCodes: [security.Code], // reporting purpose
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
                if (!sizing.CalculatePreserveFixed(services.Security, services.Portfolio, quoteCode, algoParams.OpenPositionQuantityHint))
                {
                    // try to reconcilate first
                    var recon  = new Reconcilation(core.Context);
                    await recon.ReconcileAssets();
                    if (!sizing.CalculatePreserveFixed(services.Security, services.Portfolio, quoteCode, algoParams.OpenPositionQuantityHint))
                    {
                        return BadRequest();
                    }
                }
                break;
            case PositionSizingMethod.Fixed:
                sizing.CalculateFixed(services.Security, services.Portfolio, quoteCode, algoParams.OpenPositionQuantityHint);
                break;
        }
        var algoSession = await core.Run(ep, ap, algorithm);
        var result = new Dictionary<string, object>
        {
            {"algo-session-id", algoSession.Id.ToString()}, // to avoid swagger UI's json long type lost precision issue
            {"algo-session", algoSession }
        };
        return Ok(result);
    }

    /// <summary>
    /// Only lists running algo sessions.
    /// </summary>
    /// <param name="core"></param>
    /// <param name="adminService"></param>
    /// <returns></returns>
    [HttpPost(RestApiConstants.QueryRunningAlgoSessions)]
    public ActionResult GetRunningAlgoSessions([FromServices] Core core,
                                               [FromServices] IAdminService adminService)
    {
        if (!adminService.IsLoggedIn) return BadRequest("Must login user and account first.");

        var sessions = core.GetActiveAlgoSessions();

        var results = new List<Dictionary<string, object>>();
        foreach (var session in sessions)
        {
            var result = new Dictionary<string, object>
            {
                {"algo-session-id", session.Id.ToString()}, // to avoid swagger UI's json long type lost precision issue
                {"algo-session", session }
            };
            results.Add(result);
        }
        return Ok(results);
    }

    /// <summary>
    /// Lists all algo sessions.
    /// </summary>
    /// <param name="context"></param>
    /// <returns></returns>
    [HttpPost(RestApiConstants.QueryAlgoSessions)]
    public async Task<ActionResult> GetAllAlgoSessions([FromServices] Context context)
    {
        if (!context.Services.Admin.IsLoggedIn) return BadRequest("Must login user and account first.");
        var sessions = await context.Storage.Read<AlgoSession>();

        var results = new List<Dictionary<string, object>>();
        foreach (var session in sessions)
        {
            var result = new Dictionary<string, object>
            {
                {"algo-session-id", session.Id.ToString()}, // to avoid swagger UI's json long type lost precision issue
                {"algo-session", session }
            };
            results.Add(result);
        }
        return Ok(results);
    }

    /// <summary>
    /// Gets all algo entries associated to a specific algo session.
    /// </summary>
    /// <param name="context"></param>
    /// <param name="sessionId"></param>
    /// <returns></returns>
    [HttpPost(RestApiConstants.QueryAlgoEntries)]
    public async Task<ActionResult> GetAlgoEntries([FromServices] Context context, [FromQuery(Name = "session-id")] long sessionId)
    {
        if (!context.Services.Admin.IsLoggedIn) return BadRequest("Must login user and account first.");
        var entries = await context.Storage.Read<AlgoEntry>(null, whereClause: $"{nameof(AlgoEntry.SessionId)} = $SessionId", ("$SessionId", sessionId));
        return Ok(entries);
    }

    [HttpPost(RestApiConstants.StopAlgorithm)]
    public async Task<ActionResult> StopAlgorithm([FromServices] Core core,
                                                  [FromServices] IAdminService adminService,
                                                  [FromQuery(Name = "session-id")] long sessionId)
    {
        if (!adminService.IsLoggedIn) return BadRequest("Must login user and account first.");

        var resultCode = await core.StopAlgorithm(sessionId);
        var result = new Dictionary<string, string>
        {
            {"algo-session-id", sessionId.ToString()},
            {"result", resultCode.ToString()},
        };
        return Ok(result);
    }

    [HttpPost(RestApiConstants.StopAllAlgorithms)]
    public async Task<ActionResult> StopAllAlgorithms([FromServices] Core core,
                                                      [FromServices] IAdminService adminService)
    {
        if (!adminService.IsLoggedIn) return BadRequest("Must login user and account first.");

        var (expected, successful) = await core.StopAllAlgorithms();
        return Ok(new Dictionary<string, int> { { "expected", expected }, { "successful", successful } });
    }

    /// <summary>
    /// Reconcile all orders, trades, assets with external system (aka broker / exchange).
    /// Cannot be executed when there are running algorithms.
    /// </summary>
    /// <param name="core"></param>
    /// <param name="adminService"></param>
    /// <param name="securityService"></param>
    /// <param name="portfolioService"></param>
    /// <param name="symbolStr"></param>
    /// <param name="securityType"></param>
    /// <returns></returns>
    [HttpPost(RestApiConstants.Reconcile)]
    public async Task<ActionResult> Reconcile([FromServices] Core core,
                                              [FromServices] IAdminService adminService,
                                              [FromServices] ISecurityService securityService,
                                              [FromServices] IPortfolioService portfolioService,
                                              [FromQuery(Name = "symbols")] string symbolStr = "BTCUSDT",
                                              [FromQuery(Name = "sec-type")] SecurityType securityType = SecurityType.Fx)
    {
        if (!Consts.SupportedSecurityTypes.Contains(securityType)) return BadRequest("Invalid security type selected.");
        if (!adminService.IsLoggedIn) return BadRequest("Must login user and account first.");
        if (core.GetActiveAlgoSessions().Count > 0) return BadRequest("Must not have any running algorithms.");

        string[] codes = symbolStr.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var securities = new List<Security>();
        foreach (string code in codes)
        {
            Security? security = securityService.GetSecurity(code);
            if (security == null || security.IsAsset) return BadRequest("Invalid code / missing security.");
            securities.Add(security);
        }

        var _reconciliation = new Reconcilation(adminService.Context);
        DateTime reconcileStart = DateTime.UtcNow.AddDays(-Consts.LookbackDayCount);
        await _reconciliation.RunAll(adminService.CurrentUser!, reconcileStart, securities);

        await portfolioService.Reload(false, true);

        return Ok("Done");
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
        [Required, DefaultValue(0.0003)]
        public decimal StopLoss { get; set; } = 0.0003m;

        /// <summary>
        /// Take profit ratio.
        /// </summary>
        [FromForm(Name = "take-profit")]
        [Required, DefaultValue(0.0006)]
        public decimal TakeProfit { get; set; } = 0.0006m;

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
        [Required, DefaultValue("BTC,USDT,BNB,USDT")]
        public string AssetCodeWhitelist { get; set; } = "BTC,USDT,BNB,USDT";

        /// <summary>
        /// In any situation if an FX quote currency is missing but an order needs to be created,
        /// use these quote currencies. First currency has higher precedence.
        /// </summary>
        [FromForm(Name = "preferred-quote-currencies")]
        [Required, DefaultValue("USDT")]
        public string PreferredQuoteCurrencies { get; set; } = "USDT";

        /// <summary>
        /// Cancel all open orders on algo start.
        /// </summary>
        [FromForm(Name = "cancel-open-orders-on-start")]
        [DefaultValue(true)]
        public bool CancelOpenOrdersOnStart { get; set; } = true;

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
        /// Let the engine record the order book data during order execution.
        /// </summary>
        [FromForm(Name = "recorder-order-book-on-execution")]
        [DefaultValue(true)]
        public bool RecordOrderBookOnExecution { get; set; } = true;
    }
}
