using Common;
using log4net;
using Microsoft.AspNetCore.Mvc;
using TradeCommon.Constants;
using TradeCommon.Essentials;
using TradeCommon.Essentials.Instruments;
using TradeDataCore.Instruments;
using TradeDataCore.MarketData;
using TradeLogicCore.Algorithms;

namespace TradePort.Controllers;

[ApiController]
[Route("logic/back-testing")]
public class BackTestingController : Controller
{
    private static readonly ILog _log = Logger.New();

    /// <summary>
    /// Run RUMI using 100K as initial cash.
    /// </summary>
    /// <param name="securityService"></param>
    /// <param name="mds"></param>
    /// <param name="code">Security code.</param>
    /// <param name="startStr">In yyyyMMdd</param>
    /// <param name="endStr">In yyyyMMdd</param>
    /// <param name="exchangeStr">Exchange of the security.</param>
    /// <param name="secTypeStr">Security type like Equity/Fx.</param>
    /// <param name="intervalStr">Interval of the OHLC entry like 1h/1d.</param>
    /// <param name="stopLossRatio">Checks the SLPrice=EnterPrice*(1-SLRatio) against OHLC's low price. If hits below, close immediately at the SLPrice.</param>
    /// <param name="fastParam">Fast SMA param.</param>
    /// <param name="slowParam">Slow EMA param.</param>
    /// <param name="rumiParam">RUMI SMA param (as of diff of Slow-Fast)</param>
    /// <returns></returns>
    [HttpGet("rumi")]
    public async Task<IActionResult> Rumi([FromServices] ISecurityService securityService,
                                          [FromServices] IHistoricalMarketDataService mds,
                                          [FromQuery(Name = "code")] string? code = "BTCTUSD",
                                          [FromQuery(Name = "exchange")] string exchangeStr = ExternalNames.Binance,
                                          [FromQuery(Name = "sec-type")] string? secTypeStr = "fx",
                                          [FromQuery(Name = "start")] string startStr = "20220101",
                                          [FromQuery(Name = "end")] string endStr = "20230701",
                                          [FromQuery(Name = "interval")] string? intervalStr = "1h",
                                          [FromQuery(Name = "stop-loss-ratio")] decimal stopLossRatio = 0.02m,
                                          [FromQuery(Name = "fast")] int fastParam = 3,
                                          [FromQuery(Name = "slow")] int slowParam = 5,
                                          [FromQuery(Name = "rumi")] int rumiParam = 3)
    {
        var interval = IntervalTypeConverter.Parse(intervalStr);
        if (interval == IntervalType.Unknown)
            return BadRequest("Invalid interval string.");
        if (interval == IntervalType.OneMinute || interval == IntervalType.OneHour) // TODO
            return BadRequest("Does not support yet.");

        var secType = SecurityTypeConverter.Parse(secTypeStr);
        if (secType == SecurityType.Unknown)
            return BadRequest("Invalid sec-type string.");
        var exchange = ExchangeTypeConverter.Parse(exchangeStr);
        if (exchange == ExchangeType.Unknown)
            return BadRequest("Invalid exchange string.");
        var start = startStr.ParseDate();
        if (start == DateTime.MinValue)
            return BadRequest("Invalid start date-time.");
        var end = endStr.ParseDate();
        if (end == DateTime.MinValue)
            return BadRequest("Invalid end date-time.");

        var security = await securityService.GetSecurity(code ?? "", ExchangeType.Binance, SecurityType.Fx);
        if (security == null)
            return BadRequest("Invalid security.");

        // TODO hardcode
        security.PriceDecimalPoints = 6;

        var algo = new Rumi(mds)
        {
            FastParam = fastParam,
            SlowParam = slowParam,
            RumiParam = rumiParam,
            StopLossRatio = stopLossRatio
        };
        var initCash = 100000;
        var positions = await algo.BackTest(security, interval, new DateTime(2022, 1, 1), new DateTime(2023, 6, 30), initCash);
        
        _log.Info($"Balance: {initCash}->{algo.FreeCash}");

        var result = new Dictionary<string, object>
        {
            { "InitialCash", initCash },
            { "EndCash", algo.FreeCash },
            { "Results", positions },
        };
        return Ok(positions);
    }
}
