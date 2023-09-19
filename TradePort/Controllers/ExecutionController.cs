using Autofac;
using Common;
using Microsoft.AspNetCore.Mvc;
using System;
using TradeCommon.Constants;
using TradeCommon.Essentials;
using TradeCommon.Essentials.Instruments;
using TradeCommon.Essentials.Trading;
using TradeCommon.Runtime;
using TradeCommon.Utils.Common;
using TradeDataCore.Instruments;
using TradeLogicCore;
using TradeLogicCore.Algorithms;
using TradeLogicCore.Algorithms.Parameters;
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
    private readonly ISecurityService _securityService;

    public ExecutionController(ISecurityService securityService)
    {
        _securityService = securityService;
    }


    /// <summary>
    /// Manually send an order.
    /// </summary>
    /// <param name="securityService"></param>
    /// <param name="orderService"></param>
    /// <param name="adminService"></param>
    /// <param name="portfolioService"></param>
    /// <param name="context"></param>
    /// <param name="password">Required.</param>
    /// <param name="exchange"></param>
    /// <param name="environment"></param>
    /// <param name="accountName">Account name.</param>
    /// <param name="secTypeStr">Security type.</param>
    /// <param name="symbol">Symbol of security.</param>
    /// <param name="side">Side of order.</param>
    /// <param name="orderType">Type of order. Only Limit/Market/StopLimit/StopMarket orders are supported.</param>
    /// <param name="price">Price of order. If Market order this is ignored; otherwise it must be > 0.</param>
    /// <param name="quantity">Quantity of order. Must be > 0.</param>
    /// <param name="stopLoss">Stop loss price of order. Must be > 0.</param>
    /// <param name="isFakeOrder">Send a fake order if true.</param>
    /// <returns></returns>
    [HttpPost("{exchange}/accounts/{account}/orders/send")]
    public async Task<ActionResult> SendOrder([FromServices] ISecurityService securityService,
                                              [FromServices] IOrderService orderService,
                                              [FromServices] IAdminService adminService,
                                              [FromServices] IPortfolioService portfolioService,
                                              [FromServices] Context context,
                                              [FromForm] string password,
                                              [FromRoute(Name = "exchange")] ExchangeType exchange = ExchangeType.Binance,
                                              [FromRoute(Name = "account")] string accountName = "",
                                              [FromQuery(Name = "environment")] EnvironmentType environment = EnvironmentType.Test,
                                              [FromQuery(Name = "sec-type")] string? secTypeStr = "fx",
                                              [FromQuery(Name = "symbol")] string symbol = "BTCTUSD",
                                              [FromQuery(Name = "side")] Side side = Side.None,
                                              [FromQuery(Name = "order-type")] OrderType orderType = OrderType.Limit,
                                              [FromQuery(Name = "price")] decimal price = 0,
                                              [FromQuery(Name = "quantity")] decimal quantity = 0,
                                              [FromQuery(Name = "stop-loss")] decimal stopLoss = 0.002m,
                                              [FromQuery(Name = "fake")] bool isFakeOrder = true)
    {
        if (ControllerValidator.IsAdminPasswordBad(password, out var br)) return br;
        if (ControllerValidator.IsUnknown(environment, out br)) return br;
        if (ControllerValidator.IsUnknown(exchange, out br)) return br;
        if (ControllerValidator.IsBadOrParse(secTypeStr, out SecurityType secType, out br)) return br;
        if (side == Side.None) return BadRequest("Invalid side.");
        if (ControllerValidator.IsDecimalNegative(price, out br)) return br;
        if (ControllerValidator.IsDecimalNegativeOrZero(quantity, out br)) return br;
        if (ControllerValidator.IsDecimalNegativeOrZero(stopLoss, out br)) return br;

        var security = await securityService.GetSecurity(symbol, exchange, secType);
        if (security == null) return BadRequest("Cannot find security.");

        var supportedOrderType = new List<OrderType> { OrderType.Limit, OrderType.Market, OrderType.StopLimit, OrderType.Stop, OrderType.TakeProfit, OrderType.TakeProfitLimit };
        if (!supportedOrderType.Contains(orderType))
            return BadRequest("Currently only supports normal/stop/take-profit limit/market market orders.");

        if (price == 0 && orderType != OrderType.Market)
            return BadRequest("Only market order can have zero price.");

        if (accountName.IsBlank() && context.Account == null)
        {
            return BadRequest("Either provide an account name, or login first.");
        }
        accountName = accountName.IsBlank() ? context.Account!.Name : accountName;
        var account = await adminService.GetAccount(accountName, environment);
        if (account == null) return BadRequest("Invalid or missing account.");

        var order = orderService.CreateManualOrder(security, account.Id, price, quantity, side, orderType);
        // TODO validate the remaining balance by rules defined from portfolio service only
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
    /// <param name="orderService"></param>
    /// <param name="context"></param>
    /// <param name="password"></param>
    /// <param name="accountName"></param>
    /// <param name="exchange"></param>
    /// <param name="environment"></param>
    /// <param name="secTypeStr"></param>
    /// <param name="symbol"></param>
    /// <returns></returns>
    [HttpPost("{exchange}/accounts/{account}/orders/query")]
    public async Task<ActionResult> GetAllOrders([FromServices] ISecurityService securityService,
                                                 [FromServices] IOrderService orderService,
                                                 [FromServices] Context context,
                                                 [FromForm(Name = "admin-password")] string password,
                                                 [FromRoute(Name = "account")] string accountName = "",
                                                 [FromRoute(Name = "exchange")] ExchangeType exchange = ExchangeType.Binance,
                                                 [FromQuery(Name = "environment")] EnvironmentType environment = EnvironmentType.Test,
                                                 [FromQuery(Name = "sec-type")] string? secTypeStr = "fx",
                                                 [FromQuery(Name = "symbol")] string symbol = "BTCTUSD")
    {
        if (ControllerValidator.IsAdminPasswordBad(password, out var br)) return br;
        if (ControllerValidator.IsUnknown(environment, out br)) return br;
        if (ControllerValidator.IsUnknown(exchange, out br)) return br;
        if (ControllerValidator.IsBadOrParse(secTypeStr, out SecurityType secType, out br)) return br;

        var security = await securityService.GetSecurity(symbol, exchange, secType);
        if (security == null) return BadRequest("Cannot find security.");
        if (context.Account == null)
        {
            return BadRequest("Must login first.");
        }

        var orders = await orderService.GetOrders(security, DateTime.MinValue, DateTime.UtcNow, true);
        return Ok(orders);
    }

    /// <summary>
    /// Cancel an open order by its id.
    /// Under construction.
    /// </summary>
    /// <returns></returns>
    [HttpDelete("{exchange}/accounts/{account}/order")]
    public async Task<ActionResult> CancelOrder([FromServices] IOrderService orderService,
                                                [FromForm(Name = "admin-password")] string adminPassword,
                                                [FromQuery(Name = "order-id")] long? orderId,
                                                [FromQuery(Name = "external-order-id")] string? externalOrderId)
    {
        if (ControllerValidator.IsAdminPasswordBad(adminPassword, out var br)) return br;
        if (Conditions.AllNull(orderId, externalOrderId)) return BadRequest("Either order id or external order id must be specified.");

        object? cancelledOrder = null;
        var a = new AutoResetEvent(true);
        orderService.OrderCancelled += OnOrderCancelled;
        if (orderId != null)
        {
            Task.Run(() => orderService.CancelOrder(orderId.Value));
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

    [HttpPost("algos/mac/start")]
    public async Task<ActionResult?> RunMac([FromServices] Core core,
                                            [FromServices] Context context,
                                            [FromServices] ISecurityService securityService,
                                            [FromServices] IAdminService adminService,
                                            [FromForm(Name = "admin-password")] string adminPassword,
                                            [FromQuery(Name = "sec-type")] string? secTypeStr = "fx",
                                            [FromQuery(Name = "symbol")] string symbol = "BTCTUSD",
                                            [FromQuery(Name = "interval")] string intervalStr = "1d",
                                            [FromQuery(Name = "fast-ma")] int fastMa = 3,
                                            [FromQuery(Name = "slow-ma")] int slowMa = 7,
                                            [FromQuery(Name = "stop-loss")] decimal stopLoss = 0.0005m,
                                            [FromQuery(Name = "take-profit")] decimal takeProfit = 0.0005m,
                                            [FromQuery(Name = "back-test-start")] string? startStr = "",
                                            [FromQuery(Name = "back-test-end")] string? endStr = "")
    {
        if (ControllerValidator.IsAdminPasswordBad(adminPassword, out var br)) return br;
        if (ControllerValidator.IsBadOrParse(secTypeStr, out SecurityType secType, out br)) return br;
        if (ControllerValidator.IsBadOrParse(intervalStr, out IntervalType interval, out br)) return br;
        if (ControllerValidator.IsIntNegativeOrZero(fastMa, out br)) return br;
        if (ControllerValidator.IsIntNegativeOrZero(slowMa, out br)) return br;
        if (ControllerValidator.IsDecimalNegative(stopLoss, out br)) return br;
        if (ControllerValidator.IsDecimalNegative(takeProfit, out br)) return br;

        if (adminService.CurrentUser == null || adminService.CurrentAccount == null)
            return BadRequest("Must login user and account");

        var security = await securityService.GetSecurity(symbol, core.Exchange, secType);
        if (security == null) return BadRequest("Invalid or missing security.");

        AlgoEffectiveTimeRange algoTimeRange;
        switch (core.Environment)
        {
            case EnvironmentType.Prod:
                algoTimeRange = AlgoEffectiveTimeRange.ForProduction(interval);
                break;
            case EnvironmentType.Test:
                if (ControllerValidator.IsBadOrParse(startStr, out DateTime start, out br)) return br;
                if (ControllerValidator.IsBadOrParse(endStr, out DateTime end, out br)) return br;
                algoTimeRange = AlgoEffectiveTimeRange.ForBackTesting(start, end);
                break;
            case EnvironmentType.Uat:
                algoTimeRange = AlgoEffectiveTimeRange.ForPaperTrading(interval);
                break;
            default:
                return BadRequest("Invalid environment.");
        }
        var parameters = new AlgoStartupParameters(core.Environment == EnvironmentType.Test,
                                                   adminService.CurrentUser.Name,
                                                   adminService.CurrentAccount.Name,
                                                   core.Environment,
                                                   core.Exchange,
                                                   core.Broker,
                                                   interval,
                                                   new List<Security> { security },
                                                   algoTimeRange);
        var algorithm = new MovingAverageCrossing(context, fastMa, slowMa, stopLoss, takeProfit);
        var screening = new SingleSecurityLogic(context, security);
        algorithm.Screening = screening;
        var guid = await core.StartAlgorithm(parameters, algorithm);
        return Ok(guid);
    }

    [HttpPost("algos/stop")]
    public async Task<ActionResult> StopAlgorithm([FromQuery(Name = "admin-password")] string? adminPassword,
                                                  [FromQuery(Name = "algo-guid")] string? algoGuidStr)
    {
        if (ControllerValidator.IsAdminPasswordBad(adminPassword, out var br)) return br;
        if (ControllerValidator.IsBadOrParse(algoGuidStr, out Guid guid, out br)) return br;
        if (!TradeLogicCore.Dependencies.IsRegistered) return BadRequest("Core is not even initialized.");

        var core = TradeLogicCore.Dependencies.ComponentContext.Resolve<Core>();
        await core.StopAlgorithm(guid);
        return Ok();
    }

    [HttpPost("algos/stop-all")]
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
