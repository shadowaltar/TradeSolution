using Microsoft.AspNetCore.Mvc;
using System.Xml.Linq;
using TradeCommon.Constants;
using TradeCommon.Essentials.Instruments;
using TradeCommon.Database;
using TradeDataCore.Importing.Yahoo;

namespace TradePort.Controllers;

/// <summary>
/// Provides static data access.
/// </summary>
[ApiController]
[Route("static")]
public class StaticDataController : Controller
{
    /// <summary>
    /// Get the count of price entries in database.
    /// </summary>
    /// <returns></returns>
    [HttpGet("securities/count")]
    public async Task<ActionResult> Count(
        [FromQuery(Name = "sec-type")] string? secTypeStr = "equity")
    {
        var secType = SecurityTypeConverter.Parse(secTypeStr);
        if (secType == SecurityType.Unknown)
            return BadRequest("Invalid sec-type string.");

        var resultSet = await Storage.Query("SELECT COUNT(Id) FROM " + DatabaseNames.GetDefinitionTableName(secType), DatabaseNames.StaticData);
        if (resultSet != null)
        {
            return Ok(resultSet.Rows[0][0]);
        }
        return BadRequest();
    }

    /// <summary>
    /// Get all security definitions in exchange.
    /// </summary>
    /// <param name="exchange"></param>
    /// <param name="secTypeStr"></param>
    /// <param name="limit"></param>
    /// <returns></returns>
    [HttpGet("securities/{exchange}")]
    public async Task<IActionResult> GetSecurities(
        string exchange = ExternalNames.Hkex,
        [FromQuery(Name = "sec-type")] string? secTypeStr = "equity",
        [FromQuery(Name = "limit")] int limit = 100)
    {
        var secType = SecurityTypeConverter.Parse(secTypeStr);
        if (secType == SecurityType.Unknown)
            return BadRequest("Invalid sec-type string.");

        var securities = await Storage.ReadSecurities(exchange, secType);

        return Ok(securities.Take(limit));
    }

    /// <summary>
    /// Get single security definition.
    /// </summary>
    /// <param name="exchange">Exchange abbreviation.</param>
    /// <param name="code">Security Code defined by exchange.</param>
    /// <param name="secTypeStr"></param>
    /// <returns></returns>
    [HttpGet("securities/{exchange}/{code}")]
    public async Task<IActionResult> GetSecurity(
        string exchange = ExternalNames.Hkex,
        string code = "00001",
        [FromQuery(Name = "sec-type")] string? secTypeStr = "equity")
    {
        var secType = SecurityTypeConverter.Parse(secTypeStr);
        if (secType == SecurityType.Unknown)
            return BadRequest("Invalid sec-type string.");

        var security = await Storage.ReadSecurity(exchange, code, secType);
        return Ok(security);
    }

    /// <summary>
    /// Get single security's financial stats.
    /// </summary>
    /// <param name="exchange">Exchange abbreviation.</param>
    /// <param name="code">Security Code defined by exchange.</param>
    /// <param name="secTypeStr"></param>
    /// <returns></returns>
    [HttpGet("financial-stats/{exchange}/{code}")]
    public async Task<IActionResult> GetFinancialStats(
        string exchange = ExternalNames.Hkex,
        string code = "00001",
        [FromQuery(Name = "sec-type")] string? secTypeStr = "equity")
    {
        var secType = SecurityTypeConverter.Parse(secTypeStr);
        if (secType == SecurityType.Unknown)
            return BadRequest("Invalid sec-type string.");

        var security = await Storage.ReadSecurity(exchange, code, secType);
        if (security == null)
            return NotFound();
        var stats = await Storage.ReadFinancialStats(security.Id);
        return Ok(stats);
    }

    /// <summary>
    /// Gets all security definitions in Binance and save to database.
    /// </summary>
    /// <returns></returns>
    [HttpGet($"securities/{ExternalNames.Binance}/get-and-save-all")]
    public async Task<IActionResult> GetAndSaveBinanceSecurityDefinition(
        [FromQuery(Name = "sec-type")] string? secTypeStr = "fx")
    {
        var secType = SecurityTypeConverter.Parse(secTypeStr);
        if (secType == SecurityType.Unknown)
            return BadRequest("Invalid sec-type string.");

        var reader = new TradeDataCore.Importing.Binance.DefinitionReader();
        var securities = await reader.ReadAndSave(secType);
        return Ok(securities?.Count);
    }

    /// <summary>
    /// Gets all security definitions in HKEX and save to database.
    /// </summary>
    /// <returns></returns>
    [HttpGet($"securities/{ExternalNames.Hkex}/get-and-save-all")]
    public async Task<IActionResult> GetAndSaveHongKongSecurityDefinition(
        [FromQuery(Name = "sec-type")] string? secTypeStr = "equity")
    {
        var secType = SecurityTypeConverter.Parse(secTypeStr);
        if (secType == SecurityType.Unknown)
            return BadRequest("Invalid sec-type string.");

        var reader = new TradeDataCore.Importing.Hkex.DefinitionReader();
        var securities = await reader.ReadAndSave(secType);
        return Ok(securities?.Count);
    }

    /// <summary>
    /// Gets all security stats in the exchange and save to database.
    /// </summary>
    /// <returns></returns>
    [HttpGet("financial-stats/{exchange}/get-and-save-all")]
    public async Task<IActionResult> GetAndSaveHongKongSecurityStats(string exchange = ExternalNames.Hkex,
        [FromQuery(Name = "sec-type")] string? secTypeStr = "equity")
    {
        var secType = SecurityTypeConverter.Parse(secTypeStr);
        if (secType == SecurityType.Unknown)
            return BadRequest("Invalid sec-type string.");

        var securities = await Storage.ReadSecurities(exchange, secType);
        var reader = new ListedOptionReader();
        var stats = await reader.ReadUnderlyingStats(securities);
        if (stats == null)
            return BadRequest("Failed to download or parse HK security stats.");

        var count = await Storage.InsertSecurityFinancialStats(stats);

        return Ok(count);
    }
}
