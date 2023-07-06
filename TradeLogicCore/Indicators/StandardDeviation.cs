using TradeDataCore.Essentials;

namespace TradeLogicCore.Indicators;
internal class StandardDeviation : PriceSeriesIndicator<double>
{
    public StandardDeviation(int period, Func<OhlcPrice, decimal>? selector = null) : base(period, selector)
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

        var x = 0d;
        var average = decimal.ToDouble(sum / Period);

        for (int i = Period - 1; i >= start; i--)
        {
            x += Math.Pow((double)_selector(ohlcPrices[i]) - average, 2);
        }

        x /= (Period - 1);
        return Math.Sqrt(x);
    }
}
