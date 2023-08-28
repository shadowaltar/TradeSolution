using Common;
using Microsoft.AspNetCore.Mvc;
using TradeCommon.Constants;
using TradeCommon.Essentials.Instruments;
using TradeCommon.Essentials.Trading;
using TradeCommon.Runtime;
using TradeDataCore.Instruments;
using TradeDataCore.StaticData;
using TradeLogicCore.Services;
using Environments = TradeCommon.Constants.Environments;

namespace TradePort.Controllers;

/// <summary>
/// Provides order execution, portfolio and account management.
/// </summary>
[ApiController]
[Route("execution")]
public class ExecutionController : Controller
{
    /// <summary>
    /// Set application environment.
    /// </summary>
    /// <param name="context"></param>
    /// <param name="password"></param>
    /// <param name="environment"></param>
    /// <returns></returns>
    [HttpPost("set-environment")]
    public ActionResult SetEnvironment([FromServices] Context context,
                                       [FromForm] string password,
                                       [FromQuery(Name = "env")] string environment = "Test")
    {
        if (password.IsBlank()) return BadRequest();
        if (!Credential.IsAdminPasswordCorrect(password)) return BadRequest();

        var type = environment.ConvertDescriptionToEnum<EnvironmentType>();
        if (type == EnvironmentType.Unknown)
            return BadRequest("Invalid environment string. It is case sensitive.");

        context.SetEnvironment(type);
        return Ok(type);
    }

    /// <summary>
    /// Manually send an order.
    /// </summary>
    /// <param name="securityService"></param>
    /// <param name="orderService"></param>
    /// <param name="adminService"></param>
    /// <param name="portfolioService"></param>
    /// <param name="password">Required.</param>
    /// <param name="exchangeStr">Exchange name.</param>
    /// <param name="envStr"></param>
    /// <param name="accountName">Account name.</param>
    /// <param name="secTypeStr">Security type.</param>
    /// <param name="symbol">Symbol of security.</param>
    /// <param name="sideStr"></param>
    /// <param name="orderTypeStr"></param>
    /// <param name="price"></param>
    /// <param name="quantity"></param>
    /// <param name="isFakeOrder">Send a fake order if true.</param>
    /// <returns></returns>
    [HttpPost("{exchange}/accounts/{account}/order")]
    public async Task<ActionResult> SendOrder(
        [FromServices] ISecurityService securityService,
        [FromServices] IOrderService orderService,
        [FromServices] IAdminService adminService,
        [FromServices] IPortfolioService portfolioService,
        [FromForm] string password,
        [FromRoute(Name = "exchange")] string exchangeStr = ExternalNames.Binance,
        [FromRoute(Name = "env")] string envStr = Environments.Unknown,
        [FromRoute(Name = "account")] string accountName = "0",
        [FromQuery(Name = "sec-type")] string? secTypeStr = "fx",
        [FromQuery(Name = "symbol")] string symbol = "BTCTUSD",
        [FromQuery(Name = "side")] string sideStr = "Buy",
        [FromQuery(Name = "order-type")] string orderTypeStr = "Limit",
        [FromQuery(Name = "price")] decimal price = 0,
        [FromQuery(Name = "quantity")] decimal quantity = 0,
        [FromQuery(Name = "fake")] bool isFakeOrder = true)
    {
        if (password.IsBlank()) return BadRequest();
        if (!Credential.IsAdminPasswordCorrect(password)) return BadRequest();

        var envType = Environments.Parse(envStr);
        if (envType == EnvironmentType.Unknown)
            return BadRequest("Invalid env-type string.");
        var secType = SecurityTypeConverter.Parse(secTypeStr);
        if (secType == SecurityType.Unknown)
            return BadRequest("Invalid sec-type string.");
        var exchange = exchangeStr.ConvertDescriptionToEnum(ExchangeType.Unknown);
        if (exchange == ExchangeType.Unknown)
            return BadRequest("Invalid exchange string.");
        var security = await securityService.GetSecurity(symbol, exchange, secType);
        if (security == null)
            return BadRequest("Cannot find security.");
        var side = SideConverter.Parse(sideStr);
        if (side == Side.None)
            return BadRequest("Invalid side string.");
        var orderType = orderTypeStr.ConvertDescriptionToEnum(OrderType.Unknown);
        if (orderType == OrderType.Unknown)
            return BadRequest("Invalid order-type string.");
        if (price <= 0 && orderType != OrderType.Market)
            return BadRequest("Only market order can have zero price.");
        if (quantity <= 0)
            return BadRequest("Does not support non-positive quantity.");

        var account = await adminService.GetAccount(accountName, envType);

        var order = orderService.CreateManualOrder(security, account.Id, price, quantity, side, orderType);
        // TODO validate the remaining balance by rules defined from portfolio service only
        if (!portfolioService.Validate(order))
        {
            return BadRequest("Invalid order price or quantity.");
        }

        // as a manual order, no need to go through algorithm position sizing rules
        orderService.SendOrder(order, isFakeOrder);
        return Ok(order);
    }

    /// <summary>
    /// Cancel an open order by its id.
    /// Under construction.
    /// </summary>
    /// <returns></returns>
    [HttpDelete("{exchange}/accounts/{account}/order")]
    public async Task<ActionResult> CancelOrder()
    {
        return Ok();
    }

    /// <summary>
    /// Get account's information.
    /// </summary>
    /// <returns></returns>
    [HttpPost("{exchange}/accounts/{account}")]
    public async Task<ActionResult> GetAccount(
        [FromServices] IAdminService adminService,
        [FromForm] string password,
        [FromRoute(Name = "env")] string envStr = Environments.Unknown,
        [FromRoute(Name = "account")] string accountName = "TEST_ACCOUNT_NAME")
    {
        var envType = Environments.Parse(envStr);
        if (envType == EnvironmentType.Unknown)
            return BadRequest("Invalid env-type string.");

        if (password.IsBlank()) return BadRequest();
        if (!Credential.IsAdminPasswordCorrect(password)) return BadRequest();

        var account = await adminService.GetAccount(accountName, envType);
        return Ok(account);
    }
}
