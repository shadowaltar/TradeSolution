using Common;
using Common.Web;
using log4net;
using Microsoft.AspNetCore.Mvc;
using System.Data;
using TradeCommon.Constants;
using TradeCommon.Database;
using TradeCommon.Essentials;
using TradeCommon.Essentials.Fundamentals;
using TradeCommon.Essentials.Instruments;
using TradeCommon.Exporting;
using TradeCommon.Runtime;
using TradeCommon.Utils;
using TradeDataCore.Essentials;
using TradeDataCore.Instruments;
using TradePort.Utils;

namespace TradePort.Controllers;

/// <summary>
/// Provides prices access.
/// </summary>
[ApiController]
[Route("prices")]
public class PriceController : Controller
{
    private static readonly ILog _log = Logger.New();

    [HttpGet("order-books/recorder/start")]
    public async Task<ActionResult> StartOrderBookRecorder([FromServices] ISecurityService securityService,
                                                           [FromForm(Name = "admin-password")] string adminPassword,
                                                           [FromQuery(Name = "security-code")] string securityCode = "BTCUSDT",
                                                           [FromQuery(Name = "exchange")] ExchangeType exchange = ExchangeType.Binance,
                                                           [FromQuery(Name = "environment")] EnvironmentType environment = EnvironmentType.Prod,
                                                           [FromQuery(Name = "level")] int level = 5)
    {
        if (ControllerValidator.IsAdminPasswordBad(adminPassword, out var br)) return br;
        if (level < 1 || level > 10) return BadRequest("Level must be within 1-10.");
        if (environment == EnvironmentType.Unknown) return BadRequest("Invalid environment");

        var security = securityService.GetSecurity(securityCode, exchange);
        if (security == null) return BadRequest("Invalid security.");

        var uniqueProcessName = $"ORDER_BOOK_RECORDER_{securityCode.ToUpperInvariant()}_{exchange.ToString().ToUpperInvariant()}_{level}";
        if (Processes.IsExistsInSystem(uniqueProcessName))
        {
            return Ok($"Process {uniqueProcessName} already exists.");
        }

        return Ok($"Process {uniqueProcessName} is just started.");
    }

    /// <summary>
    /// Get prices given exchangeStr, code, interval and start time.
    /// </summary>
    /// <param name="storage"></param>
    /// <param name="exchangeStr"></param>
    /// <param name="code"></param>
    /// <param name="secTypeStr"></param>
    /// <param name="intervalStr"></param>
    /// <param name="startStr">In yyyyMMdd</param>
    /// <param name="endStr">In yyyyMMdd</param>
    /// <returns></returns>
    [HttpGet("{exchangeStr}/{code}")]
    public async Task<ActionResult> GetPrices([FromServices] IStorage storage,
                                              string exchangeStr = ExternalNames.Hkex,
                                              string code = "00001",
                                              [FromQuery(Name = "sec-type")] string secTypeStr = nameof(SecurityType.Equity),
                                              [FromQuery(Name = "interval")] string intervalStr = "1d",
                                              [FromQuery(Name = "start")] string startStr = "20230101",
                                              [FromQuery(Name = "end")] string? endStr = null)
    {
        if (ControllerValidator.IsBadOrParse(intervalStr, out IntervalType interval, out var br)) return br;
        if (ControllerValidator.IsBadOrParse(secTypeStr, out SecurityType secType, out br)) return br;
        if (ControllerValidator.IsBadOrParse(startStr, out DateTime start, out br)) return br;
        if (ControllerValidator.IsBadOrParse(exchangeStr, out ExchangeType exchange, out br)) return br;
        DateTime end = DateTime.UtcNow;
        if (endStr != null && ControllerValidator.IsBadOrParse(endStr, out end, out br)) return br;

        var security = await storage.ReadSecurity(exchange, code, secType);
        if (security == null) return BadRequest("Missing security.");

        var prices = await storage.ReadPrices(security.Id, interval, secType, start, end);
        return Ok(prices);
    }

    /// <summary>
    /// Get prices from Yahoo given exchangeStr, code, interval and start time.
    /// </summary>
    /// <param name="storage"></param>
    /// <param name="securityService"></param>
    /// <param name="exchangeStr"></param>
    /// <param name="code"></param>
    /// <param name="secTypeStr"></param>
    /// <param name="intervalStr"></param>
    /// <param name="rangeStr"></param>
    /// <returns></returns>
    [HttpGet("yahoo/{exchangeStr}/{code}")]
    public async Task<ActionResult> GetPriceFromYahoo([FromServices] IStorage storage,
                                                      [FromServices] ISecurityService securityService,
                                                      string exchangeStr = ExternalNames.Hkex,
                                                      string code = "00001",
                                                      [FromQuery(Name = "sec-type")] string secTypeStr = "equity",
                                                      [FromQuery(Name = "interval")] string intervalStr = "1d",
                                                      [FromQuery(Name = "range")] string rangeStr = "10y")
    {
        if (ControllerValidator.IsBadOrParse(exchangeStr, out ExchangeType exchange, out var br)) return br;
        if (ControllerValidator.IsBadOrParse(intervalStr, out IntervalType interval, out br)) return br;
        if (ControllerValidator.IsBadOrParse(secTypeStr, out SecurityType secType, out br)) return br;
        if (ControllerValidator.IsBadOrParse(rangeStr, out TimeRangeType range, out br)) return br;

        var security = await securityService.GetSecurity(code, exchange, secType);
        if (security == null) return BadRequest("Missing security.");

        var prices = new TradeDataCore.Importing.Yahoo.HistoricalPriceReader(storage)
            .ReadYahooPrices(new List<Security> { security }, interval, range);
        return Ok(prices);
    }

    /// <summary>
    /// HEAVY CALL!
    /// Gets all security price data in HKEX from Yahoo and save to database.
    /// </summary>
    /// <param name="storage"></param>
    /// <param name="securityService"></param>
    /// <param name="secTypeStr"></param>
    /// <param name="intervalStr"></param>
    /// <param name="rangeStr"></param>
    /// <param name="minMarketCapStr"></param>
    /// <returns></returns>
    [HttpGet($"{ExternalNames.Hkex}/get-and-save-all")]
    public async Task<ActionResult> GetAndSaveHkexPrices([FromServices] IStorage storage,
                                                         [FromServices] ISecurityService securityService,
                                                         [FromQuery(Name = "sec-type")] string secTypeStr = "equity",
                                                         [FromQuery(Name = "interval")] string intervalStr = "1d",
                                                         [FromQuery(Name = "range")] string rangeStr = "10y",
                                                         [FromQuery(Name = "f-market-cap-min")] string minMarketCapStr = "10g")
    {
        var minMarketCap = minMarketCapStr.ParseLong();
        if (minMarketCap < 0)
            return BadRequest("Invalid market cap min as filter. Either == 0 (no filter) or larger than 0.");

        if (ControllerValidator.IsBadOrParse(intervalStr, out IntervalType interval, out var br)) return br;
        if (ControllerValidator.IsBadOrParse(secTypeStr, out SecurityType secType, out br)) return br;
        if (ControllerValidator.IsBadOrParse(rangeStr, out TimeRangeType range, out br)) return br;

        var securities = await securityService.GetSecurities(secType, ExchangeType.Hkex);
        if (securities.IsNullOrEmpty()) return BadRequest("Missing security.");

        var priceReader = new TradeDataCore.Importing.Yahoo.HistoricalPriceReader(storage);
        var allPrices = await priceReader.ReadYahooPrices(securities, interval, range, (FinancialStatType.MarketCap, minMarketCap));

        foreach (var security in securities)
        {
            if (allPrices.TryGetValue(security.Id, out var tuple))
            {
                await storage.UpsertPrices(security.Id, interval, secType, tuple.Prices);
            }
        }
        return Ok(allPrices.ToDictionary(p => p.Key, p => p.Value.Prices.Count));
    }

    /// <summary>
    /// HEAVY CALL!
    /// Gets crypto price data in Binance and save to database.
    /// </summary>
    /// <param name="securityService"></param>
    /// <param name="secTypeStr"></param>
    /// <param name="intervalStr"></param>
    /// <param name="concatenatedSymbols"></param>
    /// <param name="startStr"></param>
    /// <param name="endStr">Default is UTC Now.</param>
    /// <returns></returns>
    [HttpGet($"{ExternalNames.Binance}/get-and-save-all")]
    public async Task<ActionResult> GetAndSaveBinancePrices([FromServices] ISecurityService securityService,
                                                            [FromQuery(Name = "sec-type")] string secTypeStr = "fx",
                                                            [FromQuery(Name = "interval")] string intervalStr = "1h",
                                                            [FromQuery(Name = "symbols")] string? concatenatedSymbols = "BTCUSDT,ETHUSDT,BTCTUSD,BNBUSDT",
                                                            [FromQuery(Name = "start")] string startStr = "20220101",
                                                            [FromQuery(Name = "end")] string? endStr = null)
    {
        if (ControllerValidator.IsBadOrParse(intervalStr, out IntervalType interval, out var br)) return br;
        if (ControllerValidator.IsBadOrParse(secTypeStr, out SecurityType secType, out br)) return br;
        if (ControllerValidator.IsBadOrParse(startStr, out DateTime start, out br)) return br;
        DateTime end = DateTime.UtcNow;
        if (endStr != null && ControllerValidator.IsBadOrParse(endStr, out end, out br)) return br;

        var symbols = concatenatedSymbols?.Split(',')
            .Select(s => s?.Trim()?.ToUpperInvariant()).Where(s => !s.IsBlank()).ToList();
        if (symbols == null || symbols.Count == 0)
            return BadRequest("Missing symbols (delimited by ',').");

        var securities = await securityService.GetSecurities(secType, ExchangeType.Binance, true);
        securities = securities.Where(s => symbols!.ContainsIgnoreCase(s.Code)).ToList();
        if (securities.IsNullOrEmpty()) return BadRequest("Missing security.");

        var priceReader = new TradeDataCore.Importing.Binance.HistoricalPriceReader();

        if (interval != IntervalType.OneMinute)
        {
            var allPrices = await priceReader.ReadPrices(securities, start, end, interval);

            foreach (var security in securities)
            {
                if (allPrices?.TryGetValue(security.Id, out var list) ?? false)
                {
                    await securityService.UpsertPrices(security.Id, interval, secType, list);
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
                        var (securityId, count) = await securityService.UpsertPrices(security.Id, interval, secType, list);
                        var oldCount = summary.GetOrCreate(security.Code);
                        summary[security.Code] = oldCount + count;
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
    /// <param name="securityService"></param>
    /// <param name="intervalStr"></param>
    /// <param name="isTest"></param>
    /// <param name="code"></param>
    /// <returns></returns>
    [HttpGet($"{ExternalNames.Binance}/real-time")]
    public async Task<ActionResult> GetOneRealTimeBinancePrice([FromServices] ISecurityService securityService,
                                                               [FromQuery(Name = "interval")] string intervalStr = "1m",
                                                               [FromQuery(Name = "test")] bool isTest = false,
                                                               string? code = "BTCTUSD")
    {
        if (ControllerValidator.IsBadOrParse(intervalStr, out IntervalType interval, out var br)) return br;
        if (code.IsBlank()) return BadRequest("Missing symbol.");

        var security = await securityService.GetSecurity(code, ExchangeType.Binance, SecurityType.Fx);
        if (security == null) return BadRequest("Missing security.");

        var wsName = $"{security.Code.ToLowerInvariant()}@kline_{IntervalTypeConverter.ToIntervalString(interval).ToLowerInvariant()}";
        var url = isTest ? $"wss://testnet.binance.vision/stream?streams={wsName}" : $"wss://stream.binance.com:9443/stream?streams={wsName}";
        var result = await ExtendedWebSocket.ListenOne(url);

        return Ok(result);
    }

    /// <summary>
    /// Gets one security price data from Yahoo in this exchangeStr and save to database.
    /// </summary>
    /// <param name="securityService"></param>
    /// <param name="storage"></param>
    /// <param name="exchangeStr"></param>
    /// <param name="code"></param>
    /// <param name="secTypeStr"></param>
    /// <param name="intervalStr"></param>
    /// <param name="rangeStr"></param>
    /// <returns></returns>
    [HttpGet("{exchangeStr}/get-and-save-one")]
    public async Task<ActionResult> GetAndSaveHongKongOne([FromServices] ISecurityService securityService,
                                                          [FromServices] IStorage storage,
                                                          [FromQuery(Name = "exchange")] string exchangeStr = ExternalNames.Hkex,
                                                          string code = "00001",
                                                          [FromQuery(Name = "sec-type")] string secTypeStr = "equity",
                                                          [FromQuery(Name = "interval")] string intervalStr = "1h",
                                                          [FromQuery(Name = "range")] string rangeStr = "2y")
    {
        if (ControllerValidator.IsBadOrParse(exchangeStr, out ExchangeType exchange, out var br)) return br;
        if (ControllerValidator.IsBadOrParse(intervalStr, out IntervalType interval, out br)) return br;
        if (ControllerValidator.IsBadOrParse(secTypeStr, out SecurityType secType, out br)) return br;
        if (ControllerValidator.IsBadOrParse(rangeStr, out TimeRangeType range, out br)) return br;

        var security = await securityService.GetSecurity(code, exchange, secType);
        if (security == null) return BadRequest("Missing security.");

        var priceReader = new TradeDataCore.Importing.Yahoo.HistoricalPriceReader(storage);
        var allPrices = await priceReader.ReadYahooPrices(new List<Security> { security }, interval, range);

        if (allPrices.TryGetValue(security.Id, out var tuple))
        {
            await securityService.UpsertPrices(security.Id, interval, secType, tuple.Prices);
            var count = await storage.Query($"SELECT COUNT(Close) FROM {DatabaseNames.GetPriceTableName(interval, secType)} WHERE SecurityId = {security.Id}", DatabaseNames.MarketData);
            Console.WriteLine($"Code {security.Code} exchangeStr {security.Exchange} (Yahoo {security.YahooTicker}) price count: {tuple.Prices.Count}/{count}");
        }
        return Ok(allPrices.ToDictionary(p => p.Key, p => p.Value.Prices.Count));
    }

    /// <summary>
    /// Download all prices of all securities as JSON from the given exchangeStr.
    /// </summary>
    /// <param name="securityService"></param>
    /// <param name="exchangeStr"></param>
    /// <param name="secTypeStr">Case-insensitive, fx, equity etc.</param>
    /// <param name="intervalStr">Case-insensitive, 1m, 1h, 1d etc.</param>
    /// <param name="concatenatedSymbols">Optional security code filter, case-insensitive, comma-delimited: 00001,00002 or BTCUSDT,BTCTUSD, etc.</param>
    /// <param name="rangeStr">Case-insensitive, 1mo, 1y, 6mo etc.</param>
    /// <returns></returns>
    [HttpGet("{exchangeStr}/download-ohlc-as-json")]
    public async Task<ActionResult> DownloadAll([FromServices] ISecurityService securityService,
                                                [FromRoute(Name = "exchangeStr")] string exchangeStr = "binance",
                                                [FromQuery(Name = "sec-type")] string secTypeStr = "fx",
                                                [FromQuery(Name = "interval")] string intervalStr = "1h",
                                                [FromQuery(Name = "symbols")] string? concatenatedSymbols = "",
                                                [FromQuery(Name = "range")] string rangeStr = "2y")
    {
        if (ControllerValidator.IsBadOrParse(exchangeStr, out ExchangeType exchange, out var br)) return br;
        if (ControllerValidator.IsBadOrParse(intervalStr, out IntervalType interval, out br)) return br;
        if (ControllerValidator.IsBadOrParse(secTypeStr, out SecurityType secType, out br)) return br;
        if (ControllerValidator.IsBadOrParse(rangeStr, out TimeRangeType range, out br)) return br;

        var symbols = concatenatedSymbols?.Split(',')
            .Select(s => s?.Trim()?.ToUpperInvariant()).Where(s => !s.IsBlank()).ToList();

        var securities = await securityService.GetSecurities(secType, exchange);
        if (securities.IsNullOrEmpty()) return BadRequest("Missing security.");

        if (!symbols.IsNullOrEmpty())
        {
            securities = securities.Where(s => symbols!.ContainsIgnoreCase(s.Code)).ToList();
        }
        var start = TimeRangeTypeConverter.ConvertTimeSpan(range, OperatorType.Minus)(DateTime.Today);
        var allPrices = await securityService.ReadAllPrices(securities, interval, secType, range);

        var extendedResults = allPrices.Values.SelectMany(l => l)
            .OrderBy(i => i.Ex).ThenBy(i => i.Code).ThenBy(i => i.I).ThenBy(i => i.T)
            .ToList();

        var dataFilePath = Path.Join(Path.GetTempPath(), $"AllPrices_{intervalStr}_{start.ToString(Consts.DefaultDateFormat)}_{exchange}.json");

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
    /// <param name="storage"></param>
    /// <param name="intervalStr"></param>
    /// <param name="secTypeStr"></param>
    /// <returns></returns>
    [HttpGet("metrics/ohlc-entry-count")]
    public async Task<ActionResult> Count([FromServices] IStorage storage,
                                          [FromQuery(Name = "interval")] string intervalStr = "1h",
                                          [FromQuery(Name = "sec-type")] string secTypeStr = "equity")
    {
        if (ControllerValidator.IsBadOrParse(intervalStr, out IntervalType interval, out var br)) return br;
        if (ControllerValidator.IsBadOrParse(secTypeStr, out SecurityType secType, out br)) return br;

        var resultSet = await storage.Query("SELECT COUNT(Close) FROM " + DatabaseNames.GetPriceTableName(interval, secType), DatabaseNames.MarketData);
        return resultSet != null ? Ok(resultSet.Rows[0][0]) : BadRequest();
    }

    /// <summary>
    /// Get the count of price entries for each ticker in database.
    /// </summary>
    /// <returns></returns>
    [HttpGet("metrics/ohlc-entry-count-per-security")]
    public async Task<ActionResult> ReportPriceCount([FromServices] IStorage storage,
                                                     [FromQuery(Name = "interval")] string intervalStr = "1h",
                                                     [FromQuery(Name = "sec-type")] string secTypeStr = "equity")
    {
        if (ControllerValidator.IsBadOrParse(intervalStr, out IntervalType interval, out var br)) return br;
        if (ControllerValidator.IsBadOrParse(secTypeStr, out SecurityType secType, out br)) return br;

        var priceTableName = DatabaseNames.GetPriceTableName(interval, secType);
        var definitionTableName = DatabaseNames.GetDefinitionTableName(secType);
        if (definitionTableName.IsBlank())
            return BadRequest();
        var dt1 = await storage.Query($"select count(Close) as Count, SecurityId from {priceTableName} group by SecurityId", DatabaseNames.MarketData);
        var dt2 = await storage.Query($"select Id, Code, Exchange, Name from {definitionTableName}", DatabaseNames.StaticData);
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
    [HttpGet("metrics/ohlc-entry-count-per-day")]
    public async Task<ActionResult> ReportDailyPriceEntryCount([FromServices] IStorage storage,
                                                               [FromQuery(Name = "interval")] string intervalStr = "1h",
                                                               [FromQuery(Name = "sec-type")] string secTypeStr = "equity")
    {
        if (ControllerValidator.IsBadOrParse(intervalStr, out IntervalType interval, out var br)) return br;
        if (ControllerValidator.IsBadOrParse(secTypeStr, out SecurityType secType, out br)) return br;

        var results = await storage.ReadDailyMissingPriceSituations(interval, secType);
        return Ok(results);
    }
}
