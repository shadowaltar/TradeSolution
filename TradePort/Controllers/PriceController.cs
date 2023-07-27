using Common;
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
        var secType = SecurityTypeConverter.Parse(secTypeStr);
        if (secType == SecurityType.Unknown)
            return BadRequest("Invalid sec-type string.");

        DateTime? end = null;
        if (endStr != null)
        {
            end = endStr.ParseDate();
            if (end == null || end == DateTime.MinValue)
                return BadRequest("Invalid end date-time.");
        }
        var security = await Storage.ReadSecurity(exchange, code, secType);
        var prices = await Storage.ReadPrices(security.Id, interval, secType, start, end);
        return Ok(prices);
    }

    /// <summary>
    /// Get prices given exchange, code, interval and start time.
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
        var secType = SecurityTypeConverter.Parse(secTypeStr);
        if (secType == SecurityType.Unknown)
            return BadRequest("Invalid sec-type string.");

        var security = await Storage.ReadSecurity(exchange, code, secType);
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
        var secType = SecurityTypeConverter.Parse(secTypeStr);
        if (secType == SecurityType.Unknown)
            return BadRequest("Invalid sec-type string.");

        var securities = await Storage.ReadSecurities(ExternalNames.Hkex, secType);
        var priceReader = new TradeDataCore.Importing.Yahoo.HistoricalPriceReader();
        var allPrices = await priceReader.ReadYahooPrices(securities, interval, range, (FinancialStatType.MarketCap, minMarketCap));

        foreach (var security in securities)
        {
            if (allPrices.TryGetValue(security.Id, out var tuple))
            {
                await Storage.InsertPrices(security.Id, interval, secType, tuple.Prices);
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
        if (intervalStr.IsBlank())
            return BadRequest("Invalid interval string.");
        var interval = IntervalTypeConverter.Parse(intervalStr);
        if (interval == IntervalType.Unknown)
            return BadRequest("Invalid interval string.");
        var secType = SecurityTypeConverter.Parse(secTypeStr);
        if (secType == SecurityType.Unknown)
            return BadRequest("Invalid sec-type string.");
        if (secType == SecurityType.Unknown)
            return BadRequest("Invalid sec-type string.");
        if (startStr == null)
            return BadRequest("Missing start date-time.");
        var start = startStr.ParseDate();
        if (start == DateTime.MinValue)
            return BadRequest("Invalid start date-time.");
        DateTime end = DateTime.UtcNow;
        if (endStr != null)
        {
            end = endStr.ParseDate();
            if (end == DateTime.MinValue)
                return BadRequest("Invalid end date-time.");
        }

        var symbols = concatenatedSymbols?.Split(',')
            .Select(s => s?.Trim()?.ToUpperInvariant()).Where(s => !s.IsBlank()).ToList();
        if (symbols == null || symbols.Count == 0)
            return BadRequest("Missing symbols (delimited by ',').");

        var securities = await Storage.ReadSecurities(ExternalNames.Binance, secType);
        securities = securities.Where(s => symbols!.ContainsIgnoreCase(s.Code)).ToList();
        var priceReader = new TradeDataCore.Importing.Binance.HistoricalPriceReader();
        var allPrices = await priceReader.ReadPrices(securities, start, end, interval);

        foreach (var security in securities)
        {
            if (allPrices?.TryGetValue(security.Id, out var list) ?? false)
            {
                await Storage.InsertPrices(security.Id, interval, secType, list);
            }
        }
        return Ok(allPrices?.ToDictionary(p => p.Key, p => p.Value.Count));
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
        if (intervalStr.IsBlank())
            return BadRequest("Invalid interval string.");
        var interval = IntervalTypeConverter.Parse(intervalStr);
        if (interval == IntervalType.Unknown)
            return BadRequest("Invalid interval string.");

        if (symbol.IsBlank())
            return BadRequest("Missing symbol.");

        var security = await Storage.ReadSecurity(ExternalNames.Binance, symbol, SecurityType.Fx);

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
        var secType = SecurityTypeConverter.Parse(secTypeStr);
        if (secType == SecurityType.Unknown)
            return BadRequest("Invalid sec-type string.");

        var security = await Storage.ReadSecurity(exchange, code, secType);
        if (security == null)
            return BadRequest($"Security {code} in {exchange} is not found.");

        var priceReader = new TradeDataCore.Importing.Yahoo.HistoricalPriceReader();
        var allPrices = await priceReader.ReadYahooPrices(new List<Security> { security }, interval, range);

        if (allPrices.TryGetValue(security.Id, out var tuple))
        {
            await Storage.InsertPrices(security.Id, interval, secType, tuple.Prices);
            var count = await Storage.Query($"SELECT COUNT(Close) FROM {DatabaseNames.GetPriceTableName(interval, secType)} WHERE SecurityId = {security.Id}", DatabaseNames.MarketData);
            Console.WriteLine($"Code {security.Code} exchange {security.Exchange} (Yahoo {security.YahooTicker}) price count: {tuple.Prices.Count}/{count}");
        }
        return Ok(allPrices.ToDictionary(p => p.Key, p => p.Value.Prices.Count));
    }

    /// <summary>
    /// Download all prices of all securities as JSON from the given exchange.
    /// </summary>
    /// <param name="exchange"></param>
    /// <param name="secTypeStr"></param>
    /// <param name="intervalStr"></param>
    /// <param name="rangeStr"></param>
    /// <returns></returns>
    [HttpGet("{exchange}/download-json")]
    public async Task<ActionResult> DownloadAll(
        string exchange = ExternalNames.Hkex,
        [FromQuery(Name = "sec-type")] string secTypeStr = "equity",
        [FromQuery(Name = "interval")] string intervalStr = "1h",
        [FromQuery(Name = "range")] string rangeStr = "2y")
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
        var secType = SecurityTypeConverter.Parse(secTypeStr);
        if (secType == SecurityType.Unknown)
            return BadRequest("Invalid sec-type string.");

        var securities = await Storage.ReadSecurities(exchange, secType);
        var start = TimeRangeTypeConverter.ConvertTimeSpan(range, OperatorType.Minus)(DateTime.Today);
        var allPrices = await Storage.ReadAllPrices(securities, interval, secType, range);

        var extendedResults = allPrices.SelectMany(tuple => tuple.Value)
            .OrderBy(i => i.Ex).ThenBy(i => i.Id).ThenBy(i => i.I).ThenBy(i => i.T)
            .ToList();
        var filePath = await JsonWriter.ToJsonFile(extendedResults, $"AllPrices_{intervalStr}_{start.ToString(Constants.DefaultDateFormat)}_{exchange}.json");
        return System.IO.File.Exists(filePath)
            ? File(System.IO.File.OpenRead(filePath), "application/octet-stream", Path.GetFileName(filePath))
            : NotFound();
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
        if (intervalStr.IsBlank())
            return BadRequest("Invalid interval string.");
        var interval = IntervalTypeConverter.Parse(intervalStr);
        if (interval == IntervalType.Unknown)
            return BadRequest("Invalid interval string.");
        var secType = SecurityTypeConverter.Parse(secTypeStr);
        if (secType == SecurityType.Unknown)
            return BadRequest("Invalid sec-type string.");

        var resultSet = await Storage.Query("SELECT COUNT(Close) FROM " + DatabaseNames.GetPriceTableName(interval, secType), DatabaseNames.MarketData);
        return resultSet != null ? Ok(resultSet.Rows[0][0]) : BadRequest();
    }

    /// <summary>
    /// Get the count of price entries for each ticker in database.
    /// </summary>
    /// <returns></returns>
    [HttpGet("metrics/per-security-row-count")]
    public async Task<ActionResult> ReportPriceCount(
        [FromServices()] IHistoricalMarketDataService dataService,
        [FromQuery(Name = "interval")] string intervalStr = "1h",
        [FromQuery(Name = "sec-type")] string secTypeStr = "equity")
    {
        if (intervalStr.IsBlank())
            return BadRequest("Invalid interval string.");
        var interval = IntervalTypeConverter.Parse(intervalStr);
        if (interval == IntervalType.Unknown)
            return BadRequest("Invalid interval string.");
        var secType = SecurityTypeConverter.Parse(secTypeStr);
        if (secType == SecurityType.Unknown)
            return BadRequest("Invalid sec-type string.");

        var priceTableName = DatabaseNames.GetPriceTableName(interval, secType);
        var definitionTableName = DatabaseNames.GetDefinitionTableName(secType);
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
}
