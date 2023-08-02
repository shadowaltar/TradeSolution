using Common;
using Microsoft.AspNetCore.Mvc;
using TradeCommon.Constants;
using TradeCommon.Essentials;
using TradeCommon.Essentials.Instruments;
using TradeCommon.Essentials.Quotes;
using TradeCommon.Utils.Evaluation;
using TradeLogicCore.Indicators;
using TradeLogicCore.Instruments;

namespace TradePort.Controllers;
[ApiController]
[Route("logic/stock-screening")]
public class StockScreeningController : Controller
{
    /// <summary>
    /// Screen the security universe by OHLC close price return's standard deviation.
    /// </summary>
    /// <param name="screener"></param>
    /// <param name="count">Count of securities to be filtered in.</param>
    /// <param name="endStr">End date in yyyyMMdd or yyyyMMdd-HHmmss.</param>
    /// <param name="lookBackPeriod">OHLC entry count to trace backwards from end date.</param>
    /// <param name="excludedCodeStr">List of excluded stock codes, delimited by ",". Eg.: 00001,00002 </param>
    /// <param name="exchangeStr">Exchange of the security.</param>
    /// <param name="intervalStr">Interval of the OHLC entry like 1h/1d.</param>
    /// <param name="secTypeStr">Security type like Equity/Fx.</param>
    /// <param name="rankTypeStr">Ranking type like Top-N/Bottom-N.</param>
    /// <param name="ohlcStr">Close or Adj-Close. Default is Adj-Close.</param>
    /// <returns></returns>
    [HttpGet("by-stdev")]
    public async Task<IActionResult> ScreenByReturnStdev([FromServices] IStockScreener screener,
                                                         int count = 10,
                                                         [FromQuery(Name = "end")] string? endStr = "20230701",
                                                         int lookBackPeriod = 14,
                                                         [FromQuery(Name = "excluded-codes")] string excludedCodeStr = "",
                                                         [FromQuery(Name = "exchange")] string exchangeStr = ExternalNames.Hkex,
                                                         [FromQuery(Name = "interval")] string? intervalStr = "1h",
                                                         [FromQuery(Name = "sec-type")] string? secTypeStr = "equity",
                                                         [FromQuery(Name = "rank-type")] string? rankTypeStr = "top-n",
                                                         [FromQuery(Name = "ohlc-type")] string? ohlcStr = "adj-close")
    {
        var end = endStr.ParseDate();
        if (end == DateTime.MinValue)
            return BadRequest("Invalid start date-time.");
        var interval = IntervalTypeConverter.Parse(intervalStr);
        if (interval == IntervalType.Unknown)
            return BadRequest("Invalid interval string.");
        var secType = SecurityTypeConverter.Parse(secTypeStr);
        if (secType == SecurityType.Unknown)
            return BadRequest("Invalid sec-type string.");
        var rankType = RankingTypeConverter.Parse(rankTypeStr);
        if (rankType == RankingType.None)
            return BadRequest("Must specify ranking type like top-n or bottom-n.");
        var exchange = ExchangeTypeConverter.Parse(exchangeStr);
        if (exchange == ExchangeType.Unknown)
            return BadRequest("Invalid exchange string.");
        var excludedCodes = excludedCodeStr.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim()).ToList();
        var elementType = PriceElementTypeConverter.Parse(ohlcStr);

        var stdevIndicator = new StandardDeviationEvaluator(lookBackPeriod);
        var criteria = new OhlcPriceScreeningCriteria
        {
            IntervalType = interval,
            SecurityType = secType,
            ElementType = elementType,
            EndTime = end,
            LookBackPeriod = lookBackPeriod,
            RankingSortingType = SortingType.Descending,
            Aggregator = new Func<IList<double>, double>((returns) => stdevIndicator.Calculate(returns)),
            RankingType = rankType,
            RankingCount = count,
        };
        criteria.ExcludedCodes.AddRange(excludedCodes);

        var r = await screener.Filter(exchange, criteria);
        return Ok(r);
    }
}
