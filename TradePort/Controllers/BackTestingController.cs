using Common;
using log4net;
using Microsoft.AspNetCore.Mvc;
using TradeCommon.Calculations;
using TradeCommon.Constants;
using TradeCommon.Essentials;
using TradeCommon.Essentials.Instruments;
using TradeLogicCore.Algorithms;
using TradeLogicCore.Services;

namespace TradePort.Controllers;

[ApiController]
[Route("logic/back-testing")]
public class BackTestingController : Controller
{
    //private static readonly ILog _log = Logger.New();

    //private static readonly string rootFolder = @"C:\Temp";

    ///// <summary>
    ///// Back-test RUMI using 100K as initial cash.
    ///// </summary>
    ///// <param name="services"></param>
    ///// <param name="code">Security code.</param>
    ///// <param name="startStr">In yyyyMMdd</param>
    ///// <param name="endStr">In yyyyMMdd</param>
    ///// <param name="exchangeStr">Exchange of the security.</param>
    ///// <param name="secTypeStr">Security type like Equity/Fx.</param>
    ///// <param name="intervalStr">Interval of the OHLC entry like 1h/1d.</param>
    ///// <param name="stopLossRatio">Checks the StopLossPrice=EnterPrice*(1-SLRatio) against OHLC's low price. If hits below, close immediately at the StopLossPrice.</param>
    ///// <param name="fastParam">Fast SMA param.</param>
    ///// <param name="slowParam">Slow EMA param.</param>
    ///// <param name="rumiParam">RUMI SMA param (as of diff of Slow-Fast)</param>
    ///// <returns></returns>
    //[HttpGet("rumi/single")]
    //public async Task<IActionResult> RunSingleRumi([FromServices] IServices services,
    //                                               [FromQuery(Name = "code")] string? code = "BTCTUSD",
    //                                               [FromQuery(Name = "exchange")] string exchangeStr = ExternalNames.Binance,
    //                                               [FromQuery(Name = "sec-type")] string? secTypeStr = "fx",
    //                                               [FromQuery(Name = "start")] string startStr = "20220101",
    //                                               [FromQuery(Name = "end")] string endStr = "20230701",
    //                                               [FromQuery(Name = "interval")] string? intervalStr = "1h",
    //                                               [FromQuery(Name = "stop-loss-ratio")] decimal stopLossRatio = 0.02m,
    //                                               [FromQuery(Name = "fast")] int fastParam = 3,
    //                                               [FromQuery(Name = "slow")] int slowParam = 5,
    //                                               [FromQuery(Name = "rumi")] int rumiParam = 3)
    //{
    //    var interval = IntervalTypeConverter.Parse(intervalStr);
    //    if (interval == IntervalType.Unknown)
    //        return BadRequest("Invalid interval string.");
    //    if (interval == IntervalType.OneMinute || interval == IntervalType.OneHour) // TODO
    //        return BadRequest("Does not support yet.");

    //    var secType = SecurityTypeConverter.Parse(secTypeStr);
    //    if (secType == SecurityType.Unknown)
    //        return BadRequest("Invalid sec-type string.");
    //    var exchange = ExchangeTypeConverter.Parse(exchangeStr);
    //    if (exchange == ExchangeType.Unknown)
    //        return BadRequest("Invalid exchange string.");
    //    var start = startStr.ParseDate();
    //    if (start == DateTime.MinValue)
    //        return BadRequest("Invalid start date-time.");
    //    var end = endStr.ParseDate();
    //    if (end == DateTime.MinValue)
    //        return BadRequest("Invalid end date-time.");

    //    var security = await services.Security.GetSecurity(code ?? "", ExchangeType.Binance, SecurityType.Fx);
    //    if (security == null)
    //        return BadRequest("Invalid security.");

    //    var initCash = 100000;
    //    var algo = new Rumi(fastParam, slowParam, rumiParam, stopLossRatio);
    //    var engine = new AlgorithmEngine<RumiVariables>(services, algo);
    //    var entries = await engine.BackTest(new List<Security> { security }, interval, start, end, initCash);
    //    return Ok(entries);
    //}

    ///// <summary>
    ///// Back-test RUMI for all given combinations of parameters, using 100K as initial cash.
    ///// </summary>
    ///// <param name="services"></param>
    ///// <param name="concatenatedCodes">Optional security code. Empty value will run every codes available, or else eg. 00001,00002,00003.</param>
    ///// <param name="startStr">In yyyyMMdd</param>
    ///// <param name="endStr">In yyyyMMdd</param>
    ///// <param name="exchangeStr">Exchange of the security.</param>
    ///// <param name="secTypeStr">Security type like Equity/Fx.</param>
    ///// <param name="intervalStr">Interval of the OHLC entry like 1h/1d.</param>
    ///// <param name="stopLossRatio">Checks the StopLossPrice=EnterPrice*(1-SLRatio) against OHLC's low price. If hits below, close immediately at the StopLossPrice.</param>
    ///// <param name="concatenatedFastParam">Fast SMA param.</param>
    ///// <param name="concatenatedSlowParam">Slow EMA param.</param>
    ///// <param name="concatenatedRumiParam">RUMI SMA param (as of diff of Slow-Fast)</param>
    ///// <returns></returns>
    //[HttpGet("rumi/multiple")]
    //public async Task<IActionResult> RunMultipleRumi([FromServices] IServices services,
    //                                                 [FromQuery(Name = "codes")] string? concatenatedCodes = "BTCTUSD",
    //                                                 [FromQuery(Name = "exchange")] string exchangeStr = ExternalNames.Binance,
    //                                                 [FromQuery(Name = "sec-type")] string? secTypeStr = "fx",
    //                                                 [FromQuery(Name = "start")] string startStr = "20220101",
    //                                                 [FromQuery(Name = "end")] string endStr = "20230701",
    //                                                 [FromQuery(Name = "interval")] string? intervalStr = "1h",
    //                                                 [FromQuery(Name = "stop-loss-ratio")] decimal stopLossRatio = 0.02m,
    //                                                 [FromQuery(Name = "fasts")] string concatenatedFastParam = "2",
    //                                                 [FromQuery(Name = "slows")] string concatenatedSlowParam = "5",
    //                                                 [FromQuery(Name = "rumis")] string concatenatedRumiParam = "1")
    //{
    //    var interval = IntervalTypeConverter.Parse(intervalStr);
    //    if (interval == IntervalType.Unknown)
    //        return BadRequest("Invalid interval string.");
    //    if (interval == IntervalType.OneMinute || interval == IntervalType.OneHour) // TODO
    //        return BadRequest("Does not support yet.");
    //    var secType = SecurityTypeConverter.Parse(secTypeStr);
    //    if (secType == SecurityType.Unknown)
    //        return BadRequest("Invalid sec-type string.");
    //    var exchange = ExchangeTypeConverter.Parse(exchangeStr);
    //    if (exchange == ExchangeType.Unknown)
    //        return BadRequest("Invalid exchange string.");
    //    var start = startStr.ParseDate();
    //    if (start == DateTime.MinValue)
    //        return BadRequest("Invalid start date-time.");
    //    var end = endStr.ParseDate();
    //    if (end == DateTime.MinValue)
    //        return BadRequest("Invalid end date-time.");
    //    if (stopLossRatio < 0 || stopLossRatio > 1)
    //        return BadRequest("Invalid stop-loss ratio [0~1)");

    //    var codes = concatenatedCodes?.Split(',');

    //    var securities = new List<Security>();
    //    if (codes.IsNullOrEmpty())
    //    {
    //        securities = await services.Security.GetSecurities(exchange, secType);
    //    }
    //    else
    //    {
    //        foreach (var code in codes)
    //        {
    //            var security = await services.Security.GetSecurity(code, exchange, secType);
    //            if (security == null) continue;
    //            securities.Add(security);
    //        }
    //    }
    //    if (securities.IsNullOrEmpty())
    //        return BadRequest("Invalid security.");

    //    var initCash = 100000;
    //    var fasts = concatenatedFastParam.Split(',').Use(i => int.TryParse(i, out var v) ? v : 0);
    //    var slows = concatenatedSlowParam.Split(',').Use(i => int.TryParse(i, out var v) ? v : 0);
    //    var rumis = concatenatedRumiParam.Split(',').Use(i => int.TryParse(i, out var v) ? v : 0);

    //    var zipFilePaths = new List<string>();
    //    await Parallel.ForEachAsync(fasts, async (f, t) =>
    //    {
    //        await Parallel.ForEachAsync(slows, async (s, t) =>
    //        {
    //            if (s <= f) return;
    //            foreach (var r in rumis)
    //            {
    //                var zipFilePath = await RunRumi(services, securities, f, s, r, stopLossRatio, interval, start, end, initCash);
    //                zipFilePaths.Add(zipFilePath);
    //            }
    //        });
    //    });
    //    if (zipFilePaths.Count == 0)
    //        return BadRequest("No result is generated.");
    //    if (zipFilePaths.Count == 1)
    //        return File(System.IO.File.OpenRead(zipFilePaths[0]), "application/octet-stream", Path.GetFileName(zipFilePaths[0]));

    //    var now = DateTime.Now;
    //    var finalZipFileName = $"Results-AlgorithmEngine-{now:yyyyMMdd-HHmmss}.zip";
    //    var finalZipFilePath = Path.Combine(rootFolder, finalZipFileName);
    //    var targetFolder = Path.Combine(rootFolder, $"Rumis-{now:yyyyMMdd-HHmmss}");
    //    foreach (var z in zipFilePaths)
    //    {
    //        var targetPath = Path.Combine(targetFolder, Path.GetFileName(z));
    //        System.IO.File.Move(z, targetPath);
    //    }
    //    Zip.Archive(targetFolder, finalZipFilePath);
    //    return File(System.IO.File.OpenRead(finalZipFilePath),
    //        "application/octet-stream", Path.GetFileName(finalZipFilePath));
    //}

    //private async Task<string> RunRumi(IServices services,
    //                                   List<Security> securities,
    //                                   int f,
    //                                   int s,
    //                                   int r,
    //                                   decimal stopLossRatio,
    //                                   IntervalType interval,
    //                                   DateTime start,
    //                                   DateTime end,
    //                                   int initCash)
    //{
    //    var now = DateTime.Now;
    //    var subFolder = $"AlgorithmEngine-{f},{s},{r}-{now:yyyyMMdd-HHmmss}";
    //    var zipFileName = $"Result-AlgorithmEngine-{f},{s},{r}-{now:yyyyMMdd-HHmmss}.zip";
    //    var folder = Path.Combine(rootFolder, subFolder);
    //    var zipFilePath = Path.Combine(rootFolder, zipFileName);
    //    var summaryFilePath = Path.Combine(folder, $"!Summary-{f},{s},{r}-{now:yyyyMMdd-HHmmss}.csv");
    //    var summaryRows = new List<List<object>>();
    //    await Parallel.ForEachAsync(securities, async (security, t) =>
    //    {
    //        var algo = new Rumi(f, s, r, stopLossRatio);
    //        var engine = new AlgorithmEngine<RumiVariables>(services, algo);
    //        var entries = await engine.BackTest(new List<Security> { security }, interval, start, end, initCash);
    //        if (entries.IsNullOrEmpty())
    //            return;
    //        var intervalStr = IntervalTypeConverter.ToIntervalString(interval);
    //        var filePath = Path.Combine(@"C:\Temp", subFolder, $"{security.Code}-{intervalStr}.csv");

    //        Csv.Write(entries, filePath);

    //        var result = new List<object>
    //        {
    //            security.Code,
    //            security.Name,
    //            intervalStr,
    //            engine.Portfolio.FreeCash,
    //            Metrics.GetAnnualizedReturn(initCash, engine.Portfolio.Notional.ToDouble(), start, end).ToString("P4"),
    //            entries.Max(e => e.Id),
    //            entries.Count(e => e.LongCloseType == CloseType.StopLoss),
    //            entries.Where(e => e.RealizedPnl > 0).Count(),
    //            filePath,
    //        };

    //        _log.Info($"Result: {string.Join('|', result)}");
    //        summaryRows.Add(result);
    //    });

    //    var headers = new List<string> {
    //        "SecurityCode",
    //        "SecurityName",
    //        "Interval",
    //        "EndFreeCash",
    //        "AnnualizedReturn",
    //        "PositionCount",
    //        "SL Count",
    //        "PositiveRPNL Count",
    //        "FilePath"
    //    };

    //    Csv.Write(headers, summaryRows, summaryFilePath);
    //    Zip.Archive(folder, zipFilePath);

    //    return zipFilePath;
    //}
}
