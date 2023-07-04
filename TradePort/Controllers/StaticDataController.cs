using Microsoft.AspNetCore.Mvc;
using TradeDataCore.Importing;
using TradeDataCore.StaticData;

namespace TradePort.Controllers;

[ApiController]
[Route("[controller]")]
public class StaticDataController : Controller
{
    [HttpGet("SecurityDefinitions/HongKong/StockList")]
    public async Task<IActionResult> GetHongKongStockList()
    {
        var downloader = new WebDownloader();
        await downloader.Download(
            "https://www.hkex.com.hk/eng/services/trading/securities/securitieslists/ListOfSecurities.xlsx",
            @"C:\Temp\HKEX.xlsx");
        return Ok(DateTimeOffset.Now);
    }

    [HttpGet("SecurityDefinitions/HongKong/StockList/GetAndSave")]
    public async Task<IActionResult> GetAndSaveHongKongSecurityDefinition()
    {
        var importer = new SecurityDefinitionImporter();
        await importer.DownloadAndParseHongKongSecurityDefinitions();
        return Ok(DateTimeOffset.Now);
    }
}
