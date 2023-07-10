﻿using Microsoft.AspNetCore.Mvc;
using System.Data;
using System.IO;
using TradeDataCore.Database;
using TradeDataCore.Essentials;
using TradeDataCore.Exporting;
using TradeDataCore.Importing.Yahoo;
using TradeDataCore.StaticData;
using TradeDataCore.Utils;
using System.Linq;

namespace TradePort.Controllers;

/// <summary>
/// Provides prices access.
/// </summary>
[ApiController]
[Route("prices")]
public class PriceController : Controller
{
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

        var security = await Storage.ReadSecurity(exchange, code);
        var prices = new PriceReader().ReadYahooPrices(new List<Security> { security }, interval, range);
        return Ok(prices);
    }

    /// <summary>
    /// VERY HEAVY CALL!
    /// Gets all security price data from Yahoo in this exchange and save to database.
    /// </summary>
    /// <param name="exchange"></param>
    /// <param name="intervalStr"></param>
    /// <param name="rangeStr"></param>
    /// <param name="minMarketCapStr"></param>
    /// <returns></returns>
    [HttpGet("{exchange}/get-and-save-all")]
    public async Task<ActionResult> GetAndSaveHongKong(
        string exchange = "HKEX",
        [FromQuery(Name = "interval")] string intervalStr = "1d",
        [FromQuery(Name = "range")] string rangeStr = "10y",
        [FromQuery(Name = "f-market-cap-min")] string minMarketCapStr = "10g")
    {
        var minMarketCap = minMarketCapStr.ParseLong();
        if (minMarketCap < 0)
            return BadRequest("Invalid market cap min as filter. Either == 0 (no filter) or larger than 0.");

        var securities = await Storage.ReadSecurities(exchange);
        var interval = IntervalTypeConverter.Parse(intervalStr);
        var range = TimeRangeTypeConverter.Parse(rangeStr);
        var priceReader = new PriceReader();
        var allPrices = await priceReader.ReadYahooPrices(securities, interval, range, (FinancialStatType.MarketCap, minMarketCap));

        foreach (var security in securities)
        {
            if (allPrices.TryGetValue(security.Id, out var tuple))
            {
                await Storage.InsertPrices(security.Id, intervalStr, tuple.Prices);
            }
        }
        return Ok(allPrices.ToDictionary(p => p.Key, p => p.Value.Prices.Count));
    }

    /// <summary>
    /// Gets one security price data from Yahoo in this exchange and save to database.
    /// </summary>
    /// <param name="exchange"></param>
    /// <param name="code"></param>
    /// <param name="intervalStr"></param>
    /// <param name="rangeStr"></param>
    /// <returns></returns>
    [HttpGet("{exchange}/get-and-save-one")]
    public async Task<ActionResult> GetAndSaveHongKongOne(
        string exchange = "HKEX",
        string code = "00001",
        [FromQuery(Name = "interval")] string intervalStr = "1h",
        [FromQuery(Name = "range")] string rangeStr = "2y")
    {
        var security = await Storage.ReadSecurity(exchange, code);

        var interval = IntervalTypeConverter.Parse(intervalStr);
        var range = TimeRangeTypeConverter.Parse(rangeStr);
        var priceReader = new PriceReader();
        var allPrices = await priceReader.ReadYahooPrices(new List<Security> { security }, interval, range);

        if (allPrices.TryGetValue(security.Id, out var tuple))
        {
            await Storage.InsertPrices(security.Id, intervalStr, tuple.Prices);
            var count = await Storage.Execute("SELECT COUNT(Interval) FROM " + DatabaseNames.PriceTable, DatabaseNames.MarketData);
            Console.WriteLine($"Code {security.Code} exchange {security.Exchange} (Yahoo {security.YahooTicker}) price count: {tuple.Prices.Count}/{count}");
        }
        return Ok(allPrices.ToDictionary(p => p.Key, p => p.Value.Prices.Count));
    }

    /// <summary>
    /// Gets one security price data from Yahoo in this exchange and save to database.
    /// </summary>
    /// <param name="exchange"></param>
    /// <param name="code"></param>
    /// <param name="intervalStr"></param>
    /// <param name="rangeStr"></param>
    /// <returns></returns>
    [HttpGet("{exchange}/download-json")]
    public async Task<ActionResult> DownloadAll(
        string exchange = "HKEX",
        [FromQuery(Name = "interval")] string intervalStr = "1h",
        [FromQuery(Name = "range")] string rangeStr = "2y")
    {
        var securities = await Storage.ReadSecurities(exchange);
        var idTable = await Storage.Execute("SELECT DISTINCT SecurityId FROM " + DatabaseNames.PriceTable, DatabaseNames.MarketData);

        var ids = (from DataRow dr in idTable.Rows
                   select dr["SecurityId"].ToString().ParseInt()).Distinct().ToList();
        securities = securities.Where(s => ids.Contains(s.Id)).ToList();

        var interval = IntervalTypeConverter.Parse(intervalStr);
        var range = TimeRangeTypeConverter.Parse(rangeStr);
        var start = TimeRangeTypeConverter.ConvertTimeSpan(range, OperatorType.Minus)(DateTime.Today);
        var allPrices = await Storage.ReadAllPrices(securities, interval, range);

        var secMap = securities.ToDictionary(s => s.Id, s => s);
        var extendedResults = new List<ExtendedOhlcPrice>();
        foreach (var (secId, prices) in allPrices)
        {
            if (!secMap.TryGetValue(secId, out var sec)) continue;

            foreach (var p in prices)
            {
                extendedResults.Add(new ExtendedOhlcPrice(sec.Code, sec.Exchange, p.Open, p.High, p.Low, p.Close, p.Volume, intervalStr, start));
            }
        }
        var filePath = await JsonWriter.ToJsonFile(extendedResults, $"AllPrices_{intervalStr}_{start:yyyyMMdd}_{exchange}.json");
        if (System.IO.File.Exists(filePath))
        {
            return File(System.IO.File.OpenRead(filePath), "application/octet-stream", Path.GetFileName(filePath));
        }

        return NotFound();
    }

    /// <summary>
    /// Get the count of price entries in database.
    /// </summary>
    /// <returns></returns>
    [HttpGet("metrics/count")]
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
    /// Get the count of price entries for each ticker in database.
    /// </summary>
    /// <returns></returns>
    [HttpGet("metrics/report-price/count")]
    public async Task<ActionResult> ReportPriceCount()
    {
        var dt1 = await Storage.Execute($"select count(Close) as Count, SecurityId, Interval from {DatabaseNames.PriceTable} group by SecurityId, Interval", DatabaseNames.MarketData);
        var dt2 = await Storage.Execute($"select Id, Code, Exchange, Name from {DatabaseNames.SecurityTable}", DatabaseNames.StaticData);
        if (dt1 != null && dt2 != null)
        {
            var result = from table1 in dt1.AsEnumerable()
                         join table2 in dt2.AsEnumerable() on (string)table1["SecurityId"] equals (string)table2["Id"]
                         select new
                         {
                             SecurityId = (string)table1["SecurityId"],
                             Count = (string)table1["Count"],
                             Interval = (string)table1["Interval"],
                             Code = (string)table2["Code"],
                             Exchange = (string)table2["Exchange"],
                             Name = (string)table2["Name"],
                         };
            return Ok(result);
        }
        return BadRequest();
    }
}
