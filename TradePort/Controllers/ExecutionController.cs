using Microsoft.AspNetCore.Mvc;
using TradeCommon.Constants;
using TradeCommon.Database;
using TradeCommon.Essentials.Instruments;
using TradeCommon.Essentials.Trading;
using TradeDataCore.Importing.Yahoo;
using TradeDataCore.Instruments;
using TradeDataCore.MarketData;
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
    /// Manually send an order.
    /// </summary>
    /// <returns></returns>
    [HttpPost("{exchange}/accounts/{account}/order")]
    public async Task<ActionResult> SendOrder(
        [FromServices()] ISecurityService securityService,
        [FromServices()] IOrderService orderService,
        [FromServices()] IPortfolioService portfolioService,
        string exchangeStr = ExternalNames.Binance,
        [FromQuery(Name = "sec-type")] string? secTypeStr = "fx",
        [FromQuery(Name = "symbol")] string symbol = "BTCTUSD")
    {
        var secType = SecurityTypeConverter.Parse(secTypeStr);
        if (secType == SecurityType.Unknown)
            return BadRequest("Invalid sec-type string.");

        var exchange = ExchangeTypeConverter.Parse(exchangeStr);
        if (exchange == ExchangeType.Unknown)
            return BadRequest("Invalid exchange string.");

        var security = await securityService.GetSecurity(symbol, exchange, secType);
        if (security == null)
            return BadRequest("Cannot find security.");

        var order = orderService.CreateManualOrder(security, 0, 100, Side.Buy, OrderType.Market);
        // TODO validate the remaining balance by rules defined from portfolio service only
        if (!portfolioService.Validate(order))
        {
            return BadRequest("Invalid order price or quantity.");
        }

        // as a manual order, no need to go through algorithm position sizing rules
        orderService.SendOrder(order);
        return Ok(order);
    }

    /// <summary>
    /// Get balance of 
    /// </summary>
    /// <returns></returns>
    [HttpPost("{exchange}/accounts/{account}/balance")]
    public async Task<ActionResult> CheckAccountBalance(
        [FromServices()] ISecurityService securityService,
        [FromServices()] IOrderService orderService,
        [FromServices()] IPortfolioService portfolioService,
        string exchangeStr = ExternalNames.Binance,
        [FromQuery(Name = "sec-type")] string? secTypeStr = "fx",
        [FromQuery(Name = "symbol")] string symbol = "BTCTUSD")
    {
        var secType = SecurityTypeConverter.Parse(secTypeStr);
        if (secType == SecurityType.Unknown)
            return BadRequest("Invalid sec-type string.");

        var exchange = ExchangeTypeConverter.Parse(exchangeStr);
        if (exchange == ExchangeType.Unknown)
            return BadRequest("Invalid exchange string.");

        var security = await securityService.GetSecurity(symbol, exchange, secType);
        if (security == null)
            return BadRequest("Cannot find security.");

        var order = orderService.CreateOrder(security, 0, 100, OrderType.Market);
        // TODO validate the remaining balance by rules defined from portfolio service only
        if (!portfolioService.ValidateBalance(order))
        {
            return BadRequest("Invalid order quantity.");
        }
        // as a manual order, no need to go through algorithm position sizing rules

        var resultSet = await Storage.Query("SELECT COUNT(Id) FROM " + DatabaseNames.GetDefinitionTableName(secType), DatabaseNames.StaticData);
        return resultSet != null ? Ok(resultSet.Rows[0][0]) : BadRequest();
    }
}
