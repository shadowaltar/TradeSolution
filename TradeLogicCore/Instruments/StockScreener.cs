using TradeCommon.Essentials.Instruments;
using TradeCommon.Utils.Evaluation;
using TradeDataCore.Instruments;
using TradeDataCore.MarketData;

namespace TradeLogicCore.Instruments;

public class StockScreener : ISecurityScreener
{
    public StockScreener(IHistoricalMarketDataService marketDataService, ISecurityService securityService)
    {

    }

    public List<Security> Filter(List<Security> securities, DateTime asOfTime, List<Criteria> indicators, BoolOp indicatorConjunction = BoolOp.And)
    {
        var isOk = false;
        foreach (var security in securities)
        {
            foreach (var indicator in indicators)
            {
                if (indicator.NeedsOhlcPrice())
                    var ohlcInputs = indicator.GetPriceInputs(security, asOfTime);
                if (indicator.NeedsTickPrice())
                    var ohlcInputs = indicator.GetPriceInputs(security, asOfTime);

                var isSignaled = indicator.GetBooleanResult(inputs);

                if (indicatorConjunction == BoolOp.Or && isSignaled)
                {
                    isOk = true;
                    break;
                }
                else if (indicatorConjunction == BoolOp.And && !isSignaled)
                {
                    isOk = false;
                    break;
                }
            }
        }
    }
}