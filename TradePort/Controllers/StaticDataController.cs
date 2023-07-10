using Microsoft.AspNetCore.Mvc;
using System.Xml.Linq;
using TradeDataCore.Database;
using TradeDataCore.Essentials;
using TradeDataCore.Importing;
using TradeDataCore.Importing.Yahoo;
using TradeDataCore.StaticData;

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

        var resultSet = await Storage.Execute("SELECT COUNT(Id) FROM " + DatabaseNames.GetDefinitionTableName(secType), DatabaseNames.StaticData);
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
        string exchange = "HKEX",
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
        string exchange = "HKEX",
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
    /// Gets all security definitions in HKEX and save to database.
    /// </summary>
    /// <returns></returns>
    [HttpGet("securities/HKEX/get-and-save-all")]
    public async Task<IActionResult> GetAndSaveHongKongSecurityDefinition(
        [FromQuery(Name = "sec-type")] string? secTypeStr = "equity")
    {
        var secType = SecurityTypeConverter.Parse(secTypeStr);
        if (secType == SecurityType.Unknown)
            return BadRequest("Invalid sec-type string.");

        var downloader = new WebDownloader();
        await downloader.Download(
            "https://www.hkex.com.hk/eng/services/trading/securities/securitieslists/ListOfSecurities.xlsx",
            @"C:\Temp\HKEX.xlsx");
        var importer = new SecurityDefinitionImporter();
        var securities = await importer.DownloadAndParseHongKongSecurityDefinitions();
        if (securities == null)
            return BadRequest("Failed to download or parse HK security definitions.");
        else
        {
            securities = securities.Where(e => SecurityTypeConverter.Matches(e.Type, secType)).ToList();
            await Storage.InsertStockDefinitions(securities);
        }
        return Ok(securities.Count);
    }

    /// <summary>
    /// Gets all security stats in the exchange and save to database.
    /// </summary>
    /// <returns></returns>
    [HttpGet("financial-stats/{exchange}/get-and-save-all")]
    public async Task<IActionResult> GetAndSaveHongKongSecurityStats(string exchange = "HKEX",
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
