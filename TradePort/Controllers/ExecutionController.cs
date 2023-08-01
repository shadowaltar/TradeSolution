using Common;
using Microsoft.AspNetCore.Mvc;
using TradeCommon.Constants;
using TradeCommon.Database;
using TradeCommon.Essentials.Instruments;
using TradeCommon.Essentials.Trading;
using TradeCommon.Runtime;
using TradeDataCore.Instruments;
using TradeDataCore.StaticData;
using TradeLogicCore.Services;

namespace TradePort.Controllers;

/// <summary>
/// Provides static data access.
/// </summary>
[ApiController]
[Route("execution")]
public class ExecutionController : Controller
{
    /// <summary>
    /// Set execution environment.
    /// </summary>
    /// <param name="environments"></param>
    /// <param name="password"></param>
    /// <param name="environment"></param>
    /// <returns></returns>
    [HttpPost("set-environment")]
    public ActionResult SetEnvironment([FromServices] TradeCommon.Runtime.Environments environments,
                                       [FromForm] string password,
                                       [FromQuery(Name = "env")] string environment = "Test")
    {
        if (password.IsBlank()) return BadRequest();
        if (!Credential.IsPasswordCorrect(password)) return BadRequest();

        var type = environment.ConvertDescriptionToEnum<EnvironmentType>();
        if (type == EnvironmentType.Unknown)
            return BadRequest("Invalid environment string. It is case sensitive.");

        environments.SetEnvironment(type);
        return Ok(type);
    }

    /// <summary>
    /// Manually send an order.
    /// </summary>
    /// <param name="securityService"></param>
    /// <param name="orderService"></param>
    /// <param name="portfolioService"></param>
    /// <param name="password">Required.</param>
    /// <param name="exchangeStr">Exchange name.</param>
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
        [FromServices] IPortfolioService portfolioService,
        [FromForm] string password,
        [FromRoute(Name = "exchange")] string exchangeStr = ExternalNames.Binance,
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
        if (!Credential.IsPasswordCorrect(password)) return BadRequest();

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

        // TODO account validation
        var account = portfolioService.GetAccountByName(accountName);

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
}
