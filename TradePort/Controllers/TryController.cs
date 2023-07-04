using Microsoft.AspNetCore.Mvc;
using TradeDataCore.Importing;

namespace TradePort.Controllers;
[ApiController]
[Route("[controller]")]
public class TryController : ControllerBase
{
    private static readonly string[] Summaries = new[]
    {
        "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
    };

    private readonly ILogger<TryController> _logger;

    public TryController(ILogger<TryController> logger)
    {
        _logger = logger;
    }

    [HttpGet("XLSX")]
    public async Task<IActionResult> GetXlsx()
    {
        var downloader = new WebDownloader();
        await downloader.Download(
            "https://www.hkex.com.hk/eng/services/trading/securities/securitieslists/ListOfSecurities.xlsx",
            @"C:\Temp\HKEX.xlsx");
        return Ok(DateTimeOffset.Now);
    }

    [HttpGet("Time")]
    public IActionResult GetDateTimeOffsetNow()
    {
        return Ok(DateTimeOffset.Now);
    }

    [HttpGet("{i}")]
    public IEnumerable<WeatherForecast> Get(int i)
    {
        return Enumerable.Range(1, i).Select(index => new WeatherForecast
        {
            Date = DateOnly.FromDateTime(DateTime.Now.AddDays(index)),
            TemperatureC = Random.Shared.Next(-20, 55),
            Summary = Summaries[Random.Shared.Next(Summaries.Length)]
        })
        .ToArray();
    }
}
