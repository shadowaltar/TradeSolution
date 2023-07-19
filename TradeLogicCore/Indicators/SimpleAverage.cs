using TradeDataCore.Essentials;

namespace TradeLogicCore.Indicators;
public class SimpleAverage : PriceSeriesIndicator<double>
{
    public SimpleAverage(int period, Func<OhlcPrice, decimal>? selector = null) : base(period, selector)
    {
    }

    public override double Calculate(IList<OhlcPrice> ohlcPrices, IList<object>? otherInputs = null)
    {
        var start = ohlcPrices.Count - Period;
        if (start < 0)
            return double.NaN;

        var sum = 0m;
        for (int i = Period - 1; i >= start; i--)
        {
            sum += _selector(ohlcPrices[i]);
        }
        return decimal.ToDouble(sum);
    }
}
