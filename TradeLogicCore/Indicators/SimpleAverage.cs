using TradeCommon.Essentials.Quotes;

namespace TradeLogicCore.Indicators;
public class SimpleAverage : PriceSeriesIndicator<double>
{
    public SimpleAverage(int period, PriceElementType elementToUse = PriceElementType.Close, bool calculateFromBeginning = false) : base(period, elementToUse, calculateFromBeginning)
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
            sum += ElementSelector(ohlcPrices[i]);
        }
        return decimal.ToDouble(sum);
    }
}
