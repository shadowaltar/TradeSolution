using TradeCommon.Essentials.Quotes;

namespace TradeLogicCore.Indicators;
public class StandardDeviation : PriceSeriesIndicator<double>
{
    private readonly bool _isPopulation;

    public StandardDeviation(int period,
                             PriceElementType elementToUse = PriceElementType.Close,
                             bool isPopulation = true,
                             bool calculateFromBeginning = false) : base(period, elementToUse, calculateFromBeginning)
    {
        _isPopulation = isPopulation;
    }

    public override double Calculate(IList<OhlcPrice> ohlcPrices, IList<object>? otherInputs = null)
    {
        if (!TryGetStartIndex(ohlcPrices, out var startIndex)) return double.NaN;

        var sum = 0m;
        for (int i = Period - 1; i >= startIndex; i--)
        {
            sum += ElementSelector(ohlcPrices[i]);
        }

        var x = 0d;
        var average = decimal.ToDouble(sum / Period);

        for (int i = Period - 1; i >= startIndex; i--)
        {
            x += Math.Pow((double)ElementSelector(ohlcPrices[i]) - average, 2);
        }

        if (_isPopulation)
            x /= Period;
        else
            x /= (Period - 1);
        return Math.Sqrt(x);
    }
}
