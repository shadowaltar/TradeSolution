using Microsoft.AspNetCore.Mvc;
using TradeCommon.Constants;
using TradeCommon.Essentials.Quotes;
using TradeCommon.Utils.Evaluation;
using TradeDataCore.Instruments;
using TradeDataCore.MarketData;
using TradeLogicCore.Indicators;
using TradeLogicCore.Instruments;

namespace TradePort.Controllers;
[ApiController]
[Route("logic/stock-screening")]
public class StockScreeningController : Controller
{
    [HttpGet("by-stdev")]
    public IActionResult ScreenByReturnStdev(
        [FromServices] IStockScreener screener,
        DateTime endTime,
        int lookBackPeriod = 14,
        string exchange = ExternalNames.Hkex,
        [FromQuery(Name = "interval")] string? intervalStr = "1h",
        [FromQuery(Name = "sec-type")] string? secTypeStr = "equity")
    {

        var stdevIndicator = new StandardDeviationEvaluator(lookBackPeriod);
        var criteria = new OhlcPriceScreeningCriteria
        {
            EndTime = endTime,
            LookBackPeriod = lookBackPeriod,
            Aggregator = new Func<IList<double>, double>((prices) => stdevIndicator.Calculate(prices)),
            RankingType = RankingType.TopN,
            RankingCount = 10,
        };
        var r = screener.Filter(ExchangeTypeConverter.Parse(exchange), criteria);
        return Ok(r);
    }
}
