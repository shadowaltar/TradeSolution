using Microsoft.AspNetCore.Mvc;
using TradeDataCore.Database;
using TradeDataCore.Importing;
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
    /// Get all security definitions in exchange.
    /// </summary>
    /// <param name="exchange"></param>
    /// <param name="type"></param>
    /// <param name="limit"></param>
    /// <returns></returns>
    [HttpGet("securities/{exchange}")]
    public async Task<IActionResult> GetSecurities(
        string exchange = "HKEX",
        [FromQuery(Name = "type")] string? type = "Equity",
        [FromQuery(Name = "limit")] int limit = 100)
    {
        var securities = await Storage.ReadSecurities(exchange, type);
        
        return Ok(securities.Take(limit));
    }

    /// <summary>
    /// Get single security definition.
    /// </summary>
    /// <param name="exchange">Exchange abbreviation.</param>
    /// <param name="code">Security Code defined by exchange.</param>
    /// <returns></returns>
    [HttpGet("securities/{exchange}/{code}")]
    public async Task<IActionResult> GetSecurity(
        string exchange = "HKEX",
        string code = "00001")
    {
        var security = await Storage.ReadSecurity(exchange, code);        
        return Ok(security);
    }

    /// <summary>
    /// Gets all security definitions in HKEX and save to database.
    /// </summary>
    /// <returns></returns>
    [HttpGet("securities/HKEX/get-and-save-all")]
    public async Task<IActionResult> GetAndSaveHongKongSecurityDefinition()
    {
        var downloader = new WebDownloader();
        await downloader.Download(
            "https://www.hkex.com.hk/eng/services/trading/securities/securitieslists/ListOfSecurities.xlsx",
            @"C:\Temp\HKEX.xlsx");
        var importer = new SecurityDefinitionImporter();
        await importer.DownloadAndParseHongKongSecurityDefinitions();
        return Ok(DateTimeOffset.Now);
    }
}
