using Autofac;
using Autofac.Core;
using Common;
using log4net;
using Microsoft.AspNetCore.Mvc;
using TradeCommon.Algorithms;
using TradeCommon.Constants;
using TradeCommon.Essentials;
using TradeCommon.Essentials.Instruments;
using TradeCommon.Essentials.Trading;
using TradeCommon.Runtime;
using TradeCommon.Utils.Common;
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
[Route("execution")]
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
    /// <param name="isFakeOrder">Send a fake order if true.</param>
    /// <returns></returns>
    [HttpPost("orders/send")]
    public async Task<ActionResult> SendOrder([FromServices] ISecurityService securityService,
                                              [FromServices] IOrderService orderService,
                                              [FromServices] IPortfolioService portfolioService,
                                              [FromServices] Context context,
                                              [FromForm(Name = "admin-password")] string adminPassword,
                                              [FromQuery(Name = "sec-type")] string? secTypeStr = "fx",
                                              [FromQuery(Name = "symbol")] string symbol = "BTCTUSD",
                                              [FromQuery(Name = "side")] Side side = Side.None,
                                              [FromQuery(Name = "order-type")] OrderType orderType = OrderType.Limit,
                                              [FromQuery(Name = "price")] decimal price = 0,
                                              [FromQuery(Name = "quantity")] decimal quantity = 0,
                                              [FromQuery(Name = "stop-loss")] decimal stopLoss = 0.002m,
                                              [FromQuery(Name = "fake")] bool isFakeOrder = true)
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
        await orderService.SendOrder(order, isFakeOrder);
        return Ok(order);
    }

    /// <summary>
    /// Query all orders of a symbol.
    /// </summary>
    /// <param name="securityService"></param>
    /// <param name="adminService"></param>
    /// <param name="orderService"></param>
    /// <param name="context"></param>
    /// <param name="password"></param>
    /// <param name="accountName"></param>
    /// <param name="exchange"></param>
    /// <param name="environment"></param>
    /// <param name="secTypeStr"></param>
    /// <param name="symbol"></param>
    /// <returns></returns>
    [HttpPost("orders/query-all")]
    public async Task<ActionResult> GetAllOrders([FromServices] ISecurityService securityService,
                                                 [FromServices] IAdminService adminService,
                                                 [FromServices] IOrderService orderService,
                                                 [FromServices] Context context,
                                                 [FromForm(Name = "admin-password")] string password,
                                                 [FromQuery(Name = "sec-type")] string? secTypeStr = "fx",
                                                 [FromQuery(Name = "symbol")] string symbol = "BTCTUSD")
    {
        if (ControllerValidator.IsAdminPasswordBad(password, out var br)) return br;
        if (!adminService.IsLoggedIn) return BadRequest("Must login first.");
        if (ControllerValidator.IsBadOrParse(secTypeStr, out SecurityType secType, out br)) return br;

        var security = await securityService.GetSecurity(symbol, secType);
        if (security == null) return BadRequest("Cannot find security.");
        if (context.Account == null) return BadRequest("Must login first.");

        var orders = await orderService.GetExternalOrders(security, DateTime.MinValue);
        return Ok(orders);
    }

    /// <summary>
    /// Cancel an open order by its id.
    /// Under construction.
    /// </summary>
    /// <returns></returns>
    [HttpDelete("orders/{order-id}")]
    public async Task<ActionResult> CancelOrder([FromServices] IOrderService orderService,
                                                [FromForm(Name = "admin-password")] string adminPassword,
                                                [FromRoute(Name = "order-id")] long? orderId,
                                                [FromQuery(Name = "external-order-id")] string? externalOrderId)
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
    /// Start an algorithm.
    /// </summary>
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
    /// <param name="initialAvailableQuantity"></param>
    /// <returns></returns>
    [HttpPost("algorithms/mac/start")]
    public async Task<ActionResult?> RunMac([FromServices] Core core,
                                            [FromServices] Context context,
                                            [FromServices] ISecurityService securityService,
                                            [FromServices] IPortfolioService portfolioService,
                                            [FromServices] IAdminService adminService,
                                            [FromForm(Name = "admin-password")] string adminPassword,
                                            [FromForm(Name = "symbol")] string symbol = "BTCTUSD",
                                            [FromForm(Name = "interval")] string intervalStr = "1m",
                                            [FromForm(Name = "fast-ma")] int fastMa = 3,
                                            [FromForm(Name = "slow-ma")] int slowMa = 7,
                                            [FromForm(Name = "stop-loss")] decimal stopLoss = 0.0005m,
                                            [FromForm(Name = "take-profit")] decimal takeProfit = 0.0005m,
                                            [FromForm(Name = "position-sizing-method")] PositionSizingMethod positionSizingMethod = PositionSizingMethod.PreserveFixed,
                                            [FromForm(Name = "initial-available-quote-quantity")] decimal initialAvailableQuantity = 100)
    {
        if (ControllerValidator.IsAdminPasswordBad(adminPassword, out var br)) return br;
        if (ControllerValidator.IsBadOrParse(intervalStr, out IntervalType interval, out br)) return br;
        if (ControllerValidator.IsIntNegativeOrZero(fastMa, out br)) return br;
        if (ControllerValidator.IsIntNegativeOrZero(slowMa, out br)) return br;
        if (ControllerValidator.IsDecimalNegative(stopLoss, out br)) return br;
        if (ControllerValidator.IsDecimalNegative(takeProfit, out br)) return br;
        if (!adminService.IsLoggedIn) return BadRequest("Must login user and account");

        var security = securityService.GetSecurity(symbol);
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

        var parameters = new AlgorithmParameters(false, interval, new List<Security> { security }, algoTimeRange);
        var algorithm = new MovingAverageCrossing(context, parameters, fastMa, slowMa, stopLoss, takeProfit);
        var screening = new SingleSecurityLogic(context, security);
        var sizing = new SimplePositionSizingLogic(positionSizingMethod);
        algorithm.Screening = screening;
        algorithm.Sizing = sizing;
        switch (positionSizingMethod)
        {
            case PositionSizingMethod.PreserveFixed:
                sizing.CalculatePreserveFixed(securityService, portfolioService, quoteCode, initialAvailableQuantity);
                break;
            case PositionSizingMethod.Fixed:
                sizing.CalculateFixed(securityService, portfolioService, quoteCode, initialAvailableQuantity);
                break;
        }
        var batchId = await core.Run(parameters, algorithm);
        return Ok(batchId);
    }

    [HttpPost("algorithms/list")]
    public async Task<ActionResult> GetAllAlgorithms([FromQuery(Name = "admin-password")] string? adminPassword)
    {
        if (ControllerValidator.IsAdminPasswordBad(adminPassword, out var br)) return br;
        if (!TradeLogicCore.Dependencies.IsRegistered) return BadRequest("Core is not even initialized.");

        var core = TradeLogicCore.Dependencies.ComponentContext.Resolve<Core>();
        var ids = core.List();
        return Ok(ids);
    }

    [HttpPost("algorithms/stop")]
    public async Task<ActionResult> StopAlgorithm([FromQuery(Name = "admin-password")] string? adminPassword,
                                                  [FromQuery(Name = "algo-batch-id")] long algoBatchId)
    {
        if (ControllerValidator.IsAdminPasswordBad(adminPassword, out var br)) return br;
        if (!TradeLogicCore.Dependencies.IsRegistered) return BadRequest("Core is not even initialized.");

        var core = TradeLogicCore.Dependencies.ComponentContext.Resolve<Core>();
        await core.StopAlgorithm(algoBatchId);
        return Ok();
    }

    [HttpPost("algorithms/stop-all")]
    public async Task<ActionResult> StopAllAlgorithms([FromQuery(Name = "admin-password")] string? adminPassword)
    {
        if (ControllerValidator.IsAdminPasswordBad(adminPassword, out var br)) return br;
        if (!TradeLogicCore.Dependencies.IsRegistered) return BadRequest("Core is not even initialized.");

        var core = TradeLogicCore.Dependencies.ComponentContext.Resolve<Core>();
        await core.StopAllAlgorithms();
        return Ok();
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
}
