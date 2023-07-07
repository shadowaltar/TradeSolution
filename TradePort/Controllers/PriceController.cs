using Microsoft.AspNetCore.Mvc;
using TradeDataCore.Database;
using TradeDataCore.Essentials;
using TradeDataCore.Importing.Yahoo;
using TradeDataCore.StaticData;
using TradeDataCore.Utils;

namespace TradePort.Controllers;

/// <summary>
/// Provides prices access.
/// </summary>
[ApiController]
[Route("prices")]
public class PriceController : Controller
{
    /// <summary>
    /// Get the count of price entries in database.
    /// </summary>
    /// <returns></returns>
    [HttpGet("count")]
    public async Task<ActionResult> Count()
    {
        var resultSet = await Storage.Execute("SELECT COUNT(Interval) FROM " + DatabaseNames.PriceTable, DatabaseNames.MarketData);
        if (resultSet != null)
        {
            return Ok(resultSet.Rows[0][0]);
        }
        return BadRequest();
    }

    /// <summary>
    /// Get prices given exchange, code, interval and start time.
    /// </summary>
    /// <param name="exchange"></param>
    /// <param name="code"></param>
    /// <param name="intervalStr"></param>
    /// <param name="startStr">In yyyyMMdd</param>
    /// <param name="endStr">In yyyyMMdd</param>
    /// <returns></returns>
    [HttpGet("{exchange}/{code}")]
    public async Task<ActionResult> GetPrices(string exchange = "HKEX",
        string code = "00001",
        [FromQuery(Name = "interval")] string intervalStr = "1d",
        [FromQuery(Name = "start")] string startStr = "20230101",
        [FromQuery(Name = "end")] string? endStr = null)
    {
        if (intervalStr.IsBlank())
            return BadRequest("Invalid interval string.");
        var interval = IntervalTypeConverter.Parse(intervalStr);
        if (interval == IntervalType.Unknown)
            return BadRequest("Invalid interval string.");
        if (startStr == null)
            return BadRequest("Missing start date-time.");
        var start = startStr.ParseDate();
        if (start == DateTime.MinValue)
            return BadRequest("Invalid start date-time.");

        DateTime? end = null;
        if (endStr != null)
        {
            end = endStr.ParseDate();
            if (end == null || end == DateTime.MinValue)
                return BadRequest("Invalid end date-time.");
        }
        var security = await Storage.ReadSecurity(exchange, code);
        var prices = Storage.ReadPrices(security.Id, intervalStr, start, end);
        return Ok(prices);
    }

    /// <summary>
    /// Get prices given exchange, code, interval and start time.
    /// </summary>
    /// <param name="exchange"></param>
    /// <param name="code"></param>
    /// <param name="intervalStr"></param>
    /// <param name="rangeStr"></param>
    /// <returns></returns>
    [HttpGet("yahoo/{exchange}/{code}")]
    public async Task<ActionResult> GetPriceFromYahoo(string exchange = "HKEX",
        string code = "00001",
        [FromQuery(Name = "interval")] string intervalStr = "1d",
        [FromQuery(Name = "range")] string rangeStr = "10y")
    {
        if (intervalStr.IsBlank())
            return BadRequest("Invalid interval string.");
        var interval = IntervalTypeConverter.Parse(intervalStr);
        if (interval == IntervalType.Unknown)
            return BadRequest("Invalid interval string.");
        if (rangeStr == null)
            return BadRequest("Invalid range string.");
        var range = TimeRangeTypeConverter.Parse(rangeStr);
        if (range == TimeRangeType.Unknown)
            return BadRequest("Invalid range string.");

        var symbol = Identifiers.ToYahooSymbol(exchange, code);
        var prices = new PriceReader().ReadYahooPrices(new List<string> { symbol }, interval, range);
        return Ok(prices);
    }

    /// <summary>
    /// VERY HEAVY CALL!
    /// Gets all security price data from Yahoo in this exchange and save to database.
    /// </summary>
    /// <param name="exchange"></param>
    /// <param name="intervalStr"></param>
    /// <param name="rangeStr"></param>
    /// <returns></returns>
    [HttpGet("{exchange}/get-and-save-all")]
    public async Task<ActionResult> GetAndSaveHongKong(
        string exchange = "HKEX",
        [FromQuery(Name = "interval")] string intervalStr = "1d",
        [FromQuery(Name = "range")] string rangeStr = "10y")
    {
        var securities = await Storage.ReadSecurities(exchange);
        var tickers = new Dictionary<string, Security>();
        foreach (var security in securities)
        {
            tickers[Identifiers.ToYahooSymbol(security.Code, security.Exchange)] = security;
        }

        var interval = IntervalTypeConverter.Parse(intervalStr);
        var range = TimeRangeTypeConverter.Parse(rangeStr);
        var priceReader = new TradeDataCore.Importing.Yahoo.PriceReader();
        var allPrices = await priceReader.ReadYahooPrices(tickers.Select(p => p.Key).ToList(),
            interval, range);

        foreach (var (ticker, security) in tickers)
        {
            if (allPrices.TryGetValue(ticker, out var tuple))
            {
                await Storage.InsertPrices(security.Id, intervalStr, tuple.Prices);
                var count = await Storage.Execute("SELECT COUNT(Interval) FROM " + DatabaseNames.PriceTable, DatabaseNames.MarketData);
                Console.WriteLine($"Code {security.Code} exchange {security.Exchange} (Yahoo {ticker}) price count: {tuple.Prices.Count}/{count}");
            }
        }
        return Ok(allPrices.ToDictionary(p => p.Key, p => p.Value.Prices.Count));
    }

    /// <summary>
    /// VERY HEAVY CALL!
    /// Gets all security price data from Yahoo in this exchange and save to database.
    /// </summary>
    /// <param name="exchange"></param>
    /// <param name="intervalStr"></param>
    /// <param name="rangeStr"></param>
    /// <returns></returns>
    [HttpGet("{exchange}/get-and-save-one")]
    public async Task<ActionResult> GetAndSaveHongKongOne(
        string exchange = "HKEX",
        string code = "00001",
        [FromQuery(Name = "interval")] string intervalStr = "1h",
        [FromQuery(Name = "range")] string rangeStr = "10y")
    {
        var security = await Storage.ReadSecurity(exchange, code);
        var tickers = new Dictionary<string, Security>();
        tickers[Identifiers.ToYahooSymbol(security.Code, security.Exchange)] = security;

        var interval = IntervalTypeConverter.Parse(intervalStr);
        var range = TimeRangeTypeConverter.Parse(rangeStr);
        var priceReader = new PriceReader();
        var allPrices = await priceReader.ReadYahooPrices(tickers.Select(p => p.Key).ToList(),
            interval, range);

        foreach (var (ticker, sec) in tickers)
        {
            if (allPrices.TryGetValue(ticker, out var tuple))
            {
                await Storage.InsertPrices(sec.Id, intervalStr, tuple.Prices);
                var count = await Storage.Execute("SELECT COUNT(Interval) FROM " + DatabaseNames.PriceTable, DatabaseNames.MarketData);
                Console.WriteLine($"Code {sec.Code} exchange {sec.Exchange} (Yahoo {ticker}) price count: {tuple.Prices.Count}/{count}");
            }
        }
        return Ok(allPrices.ToDictionary(p => p.Key, p => p.Value.Prices.Count));
    }
}
