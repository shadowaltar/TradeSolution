using Common;
using Microsoft.AspNetCore.Mvc;
using TradeCommon.Constants;
using TradeCommon.Database;
using TradeCommon.Essentials.Instruments;
using TradeDataCore.Importing.Yahoo;
using TradePort.Utils;

namespace TradePort.Controllers;

/// <summary>
/// Provides static data access.
/// </summary>
[ApiController]
[Route(RestApiConstants.Static)]
public class StaticDataController : Controller
{
    /// <summary>
    /// Get the count of security entries in database.
    /// </summary>
    /// <returns></returns>
    [HttpGet("securities/count")]
    public async Task<ActionResult> Count([FromServices] IStorage storage,
                                          [FromQuery(Name = "sec-type")] string? secTypeStr = "equity")
    {
        var secType = SecurityTypeConverter.Parse(secTypeStr);
        if (secType == SecurityType.Unknown)
            return BadRequest("Invalid sec-type string.");
        var tableName = DatabaseNames.GetDefinitionTableName(secType);
        if (tableName.IsBlank())
            return BadRequest();

        var resultSet = await storage.Query("SELECT COUNT(Id) FROM " + tableName, DatabaseNames.StaticData);
        return resultSet != null ? Ok(resultSet.Rows[0][0]) : BadRequest();
    }

    /// <summary>
    /// Get all security definitions in exchangeStr.
    /// </summary>
    /// <param name="storage"></param>
    /// <param name="secTypeStr"></param>
    /// <param name="limit"></param>
    /// <returns></returns>
    [HttpGet(RestApiConstants.Securities)]
    public async Task<IActionResult> GetSecurities([FromServices] IStorage storage,
                                                   [FromQuery(Name = "exchange")] ExchangeType exchange = ExchangeType.Binance,
                                                   [FromQuery(Name = "sec-type")] SecurityType securityType = SecurityType.Fx,
                                                   [FromQuery(Name = "limit")] int limit = 100)
    {

        if (ControllerValidator.IsUnknown(exchange, out var br)) return br;
        if (ControllerValidator.IsUnknown(securityType, out br)) return br;

        var securities = await storage.ReadSecurities(securityType, exchange);

        return Ok(securities.Take(limit));
    }

    /// <summary>
    /// Get single security definition.
    /// </summary>
    /// <param name="storage"></param>
    /// <param name="exchangeStr">Exchange abbreviation.</param>
    /// <param name="code">Security Code defined by exchangeStr.</param>
    /// <param name="secTypeStr"></param>
    /// <returns></returns>
    [HttpGet("securities/{exchangeStr}/{code}")]
    public async Task<IActionResult> GetSecurity([FromServices] IStorage storage,
                                                 string exchangeStr = ExternalNames.Binance,
                                                 string code = "BTCUSDT",
                                                 [FromQuery(Name = "sec-type")] string? secTypeStr = "fx")
    {
        if (ControllerValidator.IsBadOrParse(secTypeStr, out SecurityType secType, out var br)) return br;
        if (ControllerValidator.IsBadOrParse(exchangeStr, out ExchangeType exchange, out br)) return br;

        var security = await storage.ReadSecurity(exchange, code, secType);
        return Ok(security);
    }

    /// <summary>
    /// Get single security's financial stats.
    /// </summary>
    /// <param name="storage"></param>
    /// <param name="exchangeStr">Exchange abbreviation.</param>
    /// <param name="code">Security Code.</param>
    /// <param name="secTypeStr"></param>
    /// <returns></returns>
    [HttpGet("financial-stats/{exchangeStr}/{code}")]
    public async Task<IActionResult> GetFinancialStats([FromServices] IStorage storage,
                                                       string exchangeStr = ExternalNames.Binance,
                                                       string code = "BTCUSDT",
                                                       [FromQuery(Name = "sec-type")] string? secTypeStr = "fx")
    {
        if (ControllerValidator.IsBadOrParse(secTypeStr, out SecurityType secType, out var br)) return br;
        if (ControllerValidator.IsBadOrParse(exchangeStr, out ExchangeType exchange, out br)) return br;

        var security = await storage.ReadSecurity(exchange, code, secType);
        if (security == null)
            return NotFound();
        var stats = await storage.ReadFinancialStats(security.Id);
        return Ok(stats);
    }

    /// <summary>
    /// Gets all security definitions in Binance and save to database.
    /// </summary>
    /// <returns></returns>
    [HttpGet($"securities/{ExternalNames.Binance}/read-and-save")]
    public async Task<IActionResult> GetAndSaveBinanceSecurityDefinition([FromServices] IStorage storage,
                                                                         [FromQuery(Name = "sec-type")] string? secTypeStr = "fx")
    {
        if (ControllerValidator.IsBadOrParse(secTypeStr, out SecurityType secType, out var br)) return br;

        var reader = new TradeDataCore.Importing.Binance.DefinitionReader(storage);
        var securities = await reader.ReadAndSave(secType);
        return Ok(securities?.Count);
    }

    /// <summary>
    /// Gets all security definitions in HKEX and save to database.
    /// </summary>
    /// <returns></returns>
    [HttpGet($"securities/{ExternalNames.Hkex}/read-and-save")]
    public async Task<IActionResult> GetAndSaveHongKongSecurityDefinition([FromServices] IStorage storage,
                                                                          [FromQuery(Name = "sec-type")] string? secTypeStr = "equity")
    {
        var secType = SecurityTypeConverter.Parse(secTypeStr);
        if (secType == SecurityType.Unknown)
            return BadRequest("Invalid sec-type string.");

        var reader = new TradeDataCore.Importing.Hkex.DefinitionReader(storage);
        var securities = await reader.ReadAndSave(secType);
        return Ok(securities?.Count);
    }

    /// <summary>
    /// Gets all security stats from an exchange and save to database.
    /// </summary>
    /// <returns></returns>
    [HttpGet("financial-stats/get-and-save-all")]
    public async Task<IActionResult> GetAndSaveHongKongSecurityStats([FromServices] IStorage storage,
                                                                     [FromQuery(Name = "exchange")] string exchTypeStr = ExternalNames.Hkex,
                                                                     [FromQuery(Name = "sec-type")] string? secTypeStr = "equity")
    {
        if (ControllerValidator.IsBadOrParse(secTypeStr, out SecurityType secType, out var br)) return br;
        if (ControllerValidator.IsBadOrParse(exchTypeStr, out ExchangeType exchange, out br)) return br;

        var securities = await storage.ReadSecurities(secType, exchange);
        var reader = new ListedOptionReader();
        var stats = await reader.ReadUnderlyingStats(securities);
        if (stats == null)
            return BadRequest("Failed to download or parse security financial stats.");

        var count = await storage.UpsertSecurityFinancialStats(stats);

        return Ok(count);
    }
}
