using Common;
using log4net;
using Microsoft.AspNetCore.Mvc;
using System.Data;
using TradeCommon.Constants;
using TradeCommon.Database;
using TradeCommon.Essentials;
using TradeCommon.Essentials.Fundamentals;
using TradeCommon.Essentials.Instruments;
using TradeCommon.Exporting;
using TradeCommon.Utils.Common;
using TradeDataCore.Essentials;
using TradeDataCore.MarketData;

namespace TradePort.Controllers;

/// <summary>
/// Provides prices access.
/// </summary>
[ApiController]
[Route("prices")]
public class PriceController : Controller
{
    private static readonly ILog _log = Logger.New();

    /// <summary>
    /// Get prices given exchange, code, interval and start time.
    /// </summary>
    /// <param name="exchange"></param>
    /// <param name="code"></param>
    /// <param name="secTypeStr"></param>
    /// <param name="intervalStr"></param>
    /// <param name="startStr">In yyyyMMdd</param>
    /// <param name="endStr">In yyyyMMdd</param>
    /// <returns></returns>
    [HttpGet("{exchange}/{code}")]
    public async Task<ActionResult> GetPrices(string exchange = ExternalNames.Hkex,
        string code = "00001",
        [FromQuery(Name = "sec-type")] string secTypeStr = nameof(SecurityType.Equity),
        [FromQuery(Name = "interval")] string intervalStr = "1d",
        [FromQuery(Name = "start")] string startStr = "20230101",
        [FromQuery(Name = "end")] string? endStr = null)
    {
        if (IsIntervalBad(intervalStr, out var interval, out var r1)) return r1;
        if (IsSecurityTypeBad(secTypeStr, out var secType, out var r2)) return r2;
        if (IsDateBad(startStr, out var start, out var r3)) return r3;
        DateTime end = DateTime.UtcNow;
        if (endStr != null && IsDateBad(endStr, out end, out var r4)) return r4;

        var security = await Storage.ReadSecurity(exchange, code, secType);
        if (security == null) return NotFound("Security is not found.");

        var prices = await Storage.ReadPrices(security.Id, interval, secType, start, end);
        return Ok(prices);
    }

    /// <summary>
    /// Get prices from Yahoo given exchange, code, interval and start time.
    /// </summary>
    /// <param name="exchange"></param>
    /// <param name="code"></param>
    /// <param name="secTypeStr"></param>
    /// <param name="intervalStr"></param>
    /// <param name="rangeStr"></param>
    /// <returns></returns>
    [HttpGet("yahoo/{exchange}/{code}")]
    public async Task<ActionResult> GetPriceFromYahoo(string exchange = ExternalNames.Hkex,
        string code = "00001",
        [FromQuery(Name = "sec-type")] string secTypeStr = "equity",
        [FromQuery(Name = "interval")] string intervalStr = "1d",
        [FromQuery(Name = "range")] string rangeStr = "10y")
    {
        if (IsIntervalBad(intervalStr, out var interval, out var r1)) return r1;
        if (IsSecurityTypeBad(secTypeStr, out var secType, out var r2)) return r2;
        if (IsTimeRangeBad(rangeStr, out var range, out var r3)) return r3;

        var security = await Storage.ReadSecurity(exchange, code, secType);
        if (security == null) return NotFound("Security is not found.");

        var prices = new TradeDataCore.Importing.Yahoo.HistoricalPriceReader()
            .ReadYahooPrices(new List<Security> { security }, interval, range);
        return Ok(prices);
    }

    /// <summary>
    /// HEAVY CALL!
    /// Gets all security price data in HKEX from Yahoo and save to database.
    /// </summary>
    /// <param name="secTypeStr"></param>
    /// <param name="intervalStr"></param>
    /// <param name="rangeStr"></param>
    /// <param name="minMarketCapStr"></param>
    /// <returns></returns>
    [HttpGet($"{ExternalNames.Hkex}/get-and-save-all")]
    public async Task<ActionResult> GetAndSaveHkexPrices(
        [FromQuery(Name = "sec-type")] string secTypeStr = "equity",
        [FromQuery(Name = "interval")] string intervalStr = "1d",
        [FromQuery(Name = "range")] string rangeStr = "10y",
        [FromQuery(Name = "f-market-cap-min")] string minMarketCapStr = "10g")
    {
        var minMarketCap = minMarketCapStr.ParseLong();
        if (minMarketCap < 0)
            return BadRequest("Invalid market cap min as filter. Either == 0 (no filter) or larger than 0.");

        if (IsIntervalBad(intervalStr, out var interval, out var r1)) return r1;
        if (IsSecurityTypeBad(secTypeStr, out var secType, out var r2)) return r2;
        if (IsTimeRangeBad(rangeStr, out var range, out var r3)) return r3;

        var securities = await Storage.ReadSecurities(secType, ExternalNames.Hkex);
        if (securities == null) return NotFound("Security is not found.");

        var priceReader = new TradeDataCore.Importing.Yahoo.HistoricalPriceReader();
        var allPrices = await priceReader.ReadYahooPrices(securities, interval, range, (FinancialStatType.MarketCap, minMarketCap));

        foreach (var security in securities)
        {
            if (allPrices.TryGetValue(security.Id, out var tuple))
            {
                await Storage.UpsertPrices(security.Id, interval, secType, tuple.Prices);
            }
        }
        return Ok(allPrices.ToDictionary(p => p.Key, p => p.Value.Prices.Count));
    }

    /// <summary>
    /// HEAVY CALL!
    /// Gets crypto price data in Binance and save to database.
    /// </summary>
    /// <param name="secTypeStr"></param>
    /// <param name="intervalStr"></param>
    /// <param name="concatenatedSymbols"></param>
    /// <param name="startStr"></param>
    /// <param name="endStr">Default is UTC Now.</param>
    /// <returns></returns>
    [HttpGet($"{ExternalNames.Binance}/get-and-save-all")]
    public async Task<ActionResult> GetAndSaveBinancePrices(
        [FromQuery(Name = "sec-type")] string secTypeStr = "fx",
        [FromQuery(Name = "interval")] string intervalStr = "1h",
        [FromQuery(Name = "symbols")] string? concatenatedSymbols = "BTCUSDT,ETHUSDT,BNBUSDT,XRPUSDT,SOLUSDT,LTCUSDT,MATICUSDT,BCHUSDT,COMPUSDT,ARBUSDT",
        [FromQuery(Name = "start")] string startStr = "20220101",
        [FromQuery(Name = "end")] string? endStr = null)
    {
        if (IsIntervalBad(intervalStr, out var interval, out var r1)) return r1;
        if (IsSecurityTypeBad(secTypeStr, out var secType, out var r2)) return r2;
        if (IsDateBad(startStr, out var start, out var r3)) return r3;
        DateTime end = DateTime.UtcNow;
        if (endStr != null && IsDateBad(endStr, out end, out var r4)) return r4;

        var symbols = concatenatedSymbols?.Split(',')
            .Select(s => s?.Trim()?.ToUpperInvariant()).Where(s => !s.IsBlank()).ToList();
        if (symbols == null || symbols.Count == 0)
            return BadRequest("Missing symbols (delimited by ',').");

        var securities = await Storage.ReadSecurities(secType, ExternalNames.Binance);
        securities = securities.Where(s => symbols!.ContainsIgnoreCase(s.Code)).ToList();
        if (securities == null) return NotFound("Security is not found.");

        var priceReader = new TradeDataCore.Importing.Binance.HistoricalPriceReader();

        if (interval != IntervalType.OneMinute)
        {
            var allPrices = await priceReader.ReadPrices(securities, start, end, interval);

            foreach (var security in securities)
            {
                if (allPrices?.TryGetValue(security.Id, out var list) ?? false)
                {
                    await Storage.UpsertPrices(security.Id, interval, secType, list);
                }
            }
            return Ok(allPrices?.ToDictionary(p => p.Key, p => p.Value.Count));
        }
        else if (interval == IntervalType.OneMinute)
        {
            var summary = new Dictionary<string, int>();
            var tempEnd = start;
            do
            {
                tempEnd = tempEnd.AddDays(1);
                tempEnd = end < tempEnd ? end : tempEnd;
                var prices = await priceReader.ReadPrices(securities, tempEnd.AddDays(-1), tempEnd, interval);

                foreach (var security in securities)
                {
                    if (prices?.TryGetValue(security.Id, out var list) ?? false)
                    {
                        var result = await Storage.UpsertPrices(security.Id, interval, secType, list);
                        var oldCount = summary.GetOrCreate(security.Code);
                        summary[security.Code] = oldCount + result.count;
                    }
                }
                _log.Info($"Finished inserting from {tempEnd.AddDays(-1)} to {tempEnd}");
            }
            while (tempEnd < end);

            return Ok(summary);
        }
        else
        {
            return BadRequest();
        }
    }

    /// <summary>
    /// Get one piece of real-time OHLC price entry from Binance.
    /// </summary>
    /// <param name="intervalStr"></param>
    /// <param name="symbol"></param>
    /// <returns></returns>
    [HttpGet($"{ExternalNames.Binance}/real-time")]
    public async Task<ActionResult> GetOneRealTimeBinancePrice(
       [FromQuery(Name = "interval")] string intervalStr = "1m",
       [FromQuery(Name = "symbols")] string? symbol = "BTCUSDT")
    {
        if (IsIntervalBad(intervalStr, out var interval, out var r1)) return r1;

        if (symbol.IsBlank())
            return BadRequest("Missing symbol.");

        var security = await Storage.ReadSecurity(ExternalNames.Binance, symbol, SecurityType.Fx);
        if (security == null) return NotFound("Security is not found.");

        var wsName = $"{security.Code.ToLowerInvariant()}@kline_{IntervalTypeConverter.ToIntervalString(interval).ToLowerInvariant()}";
        var url = $"wss://stream.binance.com:9443/stream?streams={wsName}";
        var result = await WebSocketHelper.ListenOne(url);

        return Ok(result);
    }

    /// <summary>
    /// Gets one security price data from Yahoo in this exchange and save to database.
    /// </summary>
    /// <param name="exchange"></param>
    /// <param name="code"></param>
    /// <param name="secTypeStr"></param>
    /// <param name="intervalStr"></param>
    /// <param name="rangeStr"></param>
    /// <returns></returns>
    [HttpGet("{exchange}/get-and-save-one")]
    public async Task<ActionResult> GetAndSaveHongKongOne(
        string exchange = ExternalNames.Hkex,
        string code = "00001",
        [FromQuery(Name = "sec-type")] string secTypeStr = "equity",
        [FromQuery(Name = "interval")] string intervalStr = "1h",
        [FromQuery(Name = "range")] string rangeStr = "2y")
    {
        if (IsIntervalBad(intervalStr, out var interval, out var r1)) return r1;
        if (IsSecurityTypeBad(secTypeStr, out var secType, out var r2)) return r2;
        if (IsTimeRangeBad(rangeStr, out var range, out var r3)) return r3;

        var security = await Storage.ReadSecurity(exchange, code, secType);
        if (security == null) return NotFound("Security is not found.");

        var priceReader = new TradeDataCore.Importing.Yahoo.HistoricalPriceReader();
        var allPrices = await priceReader.ReadYahooPrices(new List<Security> { security }, interval, range);

        if (allPrices.TryGetValue(security.Id, out var tuple))
        {
            await Storage.UpsertPrices(security.Id, interval, secType, tuple.Prices);
            var count = await Storage.Query($"SELECT COUNT(Close) FROM {DatabaseNames.GetPriceTableName(interval, secType)} WHERE SecurityId = {security.Id}", DatabaseNames.MarketData);
            Console.WriteLine($"Code {security.Code} exchange {security.Exchange} (Yahoo {security.YahooTicker}) price count: {tuple.Prices.Count}/{count}");
        }
        return Ok(allPrices.ToDictionary(p => p.Key, p => p.Value.Prices.Count));
    }

    /// <summary>
    /// Download all prices of all securities as JSON from the given exchange.
    /// </summary>
    /// <param name="exchange">Case-insensitive, binance, hkex etc.</param>
    /// <param name="secTypeStr">Case-insensitive, fx, equity etc.</param>
    /// <param name="intervalStr">Case-insensitive, 1m, 1h, 1d etc.</param>
    /// <param name="concatenatedSymbols">Optional security code filter, case-insensitive, comma-delimited: 00001,00002 or BTCUSDT,BTCTUSD, etc.</param>
    /// <param name="rangeStr">Case-insensitive, 1mo, 1y, 6mo etc.</param>
    /// <returns></returns>
    [HttpGet("{exchange}/download-json")]
    public async Task<ActionResult> DownloadAll(
        string exchange = ExternalNames.Hkex,
        [FromQuery(Name = "sec-type")] string secTypeStr = "equity",
        [FromQuery(Name = "interval")] string intervalStr = "1h",
        [FromQuery(Name = "symbols")] string? concatenatedSymbols = "",
        [FromQuery(Name = "range")] string rangeStr = "2y")
    {
        if (IsIntervalBad(intervalStr, out var interval, out var r1)) return r1;
        if (IsSecurityTypeBad(secTypeStr, out var secType, out var r2)) return r2;
        if (IsTimeRangeBad(rangeStr, out var range, out var r3)) return r3;

        var symbols = concatenatedSymbols?.Split(',')
            .Select(s => s?.Trim()?.ToUpperInvariant()).Where(s => !s.IsBlank()).ToList();

        var securities = await Storage.ReadSecurities(secType, exchange);
        if (securities == null) return NotFound("Security is not found.");

        if (!symbols.IsNullOrEmpty())
        {
            securities = securities.Where(s => symbols!.ContainsIgnoreCase(s.Code)).ToList();
        }
        var start = TimeRangeTypeConverter.ConvertTimeSpan(range, OperatorType.Minus)(DateTime.Today);
        var allPrices = await Storage.ReadAllPrices(securities, interval, secType, range);

        var extendedResults = allPrices.SelectMany(tuple => tuple.Value)
            .OrderBy(i => i.Ex).ThenBy(i => i.Id).ThenBy(i => i.I).ThenBy(i => i.T)
            .ToList();

        var dataFilePath = Path.Join(Path.GetTempPath(), $"AllPrices_{intervalStr}_{start.ToString(Constants.DefaultDateFormat)}_{exchange}.json");

        try
        {
            var filePath = await JsonWriter.ToJsonFile(extendedResults, dataFilePath);
            var zipFilePath = dataFilePath.Replace(".json", ".zip");
            System.IO.File.Delete(zipFilePath);
            Zip.Archive(dataFilePath, zipFilePath);
            return System.IO.File.Exists(zipFilePath)
                ? File(System.IO.File.OpenRead(zipFilePath), "application/octet-stream", Path.GetFileName(zipFilePath))
                : BadRequest();
        }
        catch (Exception e)
        {
            _log.Error("Failed to write json, zip or send it.", e);
            return BadRequest();
        }
    }

    /// <summary>
    /// Get the count of price entries in database.
    /// </summary>
    /// <returns></returns>
    [HttpGet("metrics/all-row-count")]
    public async Task<ActionResult> Count(
        [FromQuery(Name = "interval")] string intervalStr = "1h",
        [FromQuery(Name = "sec-type")] string secTypeStr = "equity")
    {
        if (IsIntervalBad(intervalStr, out var interval, out var r1)) return r1;
        if (IsSecurityTypeBad(secTypeStr, out var secType, out var r2)) return r2;

        var resultSet = await Storage.Query("SELECT COUNT(Close) FROM " + DatabaseNames.GetPriceTableName(interval, secType), DatabaseNames.MarketData);
        return resultSet != null ? Ok(resultSet.Rows[0][0]) : BadRequest();
    }

    /// <summary>
    /// Get the count of price entries for each ticker in database.
    /// </summary>
    /// <returns></returns>
    [HttpGet("metrics/per-security-row-count")]
    public async Task<ActionResult> ReportPriceCount(
        [FromQuery(Name = "interval")] string intervalStr = "1h",
        [FromQuery(Name = "sec-type")] string secTypeStr = "equity")
    {
        if (IsIntervalBad(intervalStr, out var interval, out var r1)) return r1;
        if (IsSecurityTypeBad(secTypeStr, out var secType, out var r2)) return r2;

        var priceTableName = DatabaseNames.GetPriceTableName(interval, secType);
        var definitionTableName = DatabaseNames.GetDefinitionTableName(secType);
        if (definitionTableName.IsBlank())
            return BadRequest();
        var dt1 = await Storage.Query($"select count(Close) as Count, SecurityId from {priceTableName} group by SecurityId", DatabaseNames.MarketData);
        var dt2 = await Storage.Query($"select Id, Code, Exchange, Name from {definitionTableName}", DatabaseNames.StaticData);
        if (dt1 != null && dt2 != null)
        {
            var result = from table1 in dt1.AsEnumerable()
                         join table2 in dt2.AsEnumerable() on (string)table1["SecurityId"] equals (string)table2["Id"]
                         select new
                         {
                             SecurityId = (string)table1["SecurityId"],
                             Count = (string)table1["Count"],
                             Code = (string)table2["Code"],
                             Exchange = (string)table2["Exchange"],
                             Name = (string)table2["Name"],
                         };
            return Ok(result);
        }
        return BadRequest();
    }

    /// <summary>
    /// List the count of OHLC price entries within one day.
    /// Supports 1m, 1h, 1d.
    /// </summary>
    /// <param name="intervalStr"></param>
    /// <param name="secTypeStr"></param>
    /// <returns></returns>
    [HttpGet("metrics/daily-price-count")]
    public async Task<ActionResult> ReportDailyPriceEntryCount(
        [FromQuery(Name = "interval")] string intervalStr = "1h",
        [FromQuery(Name = "sec-type")] string secTypeStr = "equity")
    {
        if (IsIntervalBad(intervalStr, out var interval, out var r1)) return r1;
        if (IsSecurityTypeBad(secTypeStr, out var secType, out var r2)) return r2;

        var results = await Storage.ReadDailyMissingPriceSituations(interval, secType);
        return Ok(results);
    }

    private bool IsIntervalBad(string intervalStr, out IntervalType interval, out ObjectResult? result)
    {
        result = null;
        interval = IntervalTypeConverter.Parse(intervalStr);
        if (interval == IntervalType.Unknown)
        {
            result = BadRequest("Invalid interval string.");
            return true;
        }
        return false;
    }

    private bool IsSecurityTypeBad(string secTypeStr, out SecurityType securityType, out ObjectResult? result)
    {
        result = null;
        securityType = SecurityTypeConverter.Parse(secTypeStr);
        if (securityType == SecurityType.Unknown)
        {
            result = BadRequest("Invalid sec-type string.");
            return true;
        }
        return false;
    }

    private bool IsTimeRangeBad(string rangeStr, out TimeRangeType timeRangeType, out ObjectResult? result)
    {
        result = null;
        timeRangeType = TimeRangeTypeConverter.Parse(rangeStr);
        if (timeRangeType == TimeRangeType.Unknown)
        {
            result = BadRequest("Invalid time range string.");
            return true;
        }
        return false;
    }

    private bool IsDateBad(string timeString, out DateTime date, out ObjectResult? result)
    {
        result = null;
        date = timeString.ParseDate();
        if (date == DateTime.MinValue)
        {
            result = BadRequest("Invalid start date-time.");
            return true;
        }
        return false;
    }
}
