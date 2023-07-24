using TradeCommon.Essentials.Quotes;

namespace TradeLogicCore.Indicators;
public class StandardDeviationEvaluator : PriceSeriesIndicator<double>
{
    private readonly bool _isPopulation;

    public StandardDeviationEvaluator(int period,
                             PriceElementType elementToUse = PriceElementType.Close,
                             bool isPopulation = true,
                             bool calculateFromBeginning = false) : base(period, elementToUse, calculateFromBeginning)
    {
        _isPopulation = isPopulation;
    }

    public override double Calculate(IList<double> values, IList<object>? otherInputs = null)
    {
        if (!TryGetStartIndex(values, out var startIndex)) return double.NaN;

        var sum = 0d;
        for (int i = Period - 1; i >= startIndex; i--)
        {
            sum += values[i];
        }

        var x = 0d;
        var average = sum / Period;

        for (int i = Period - 1; i >= startIndex; i--)
        {
            x += Math.Pow(values[i] - average, 2);
        }

        if (_isPopulation)
            x /= Period;
        else
            x /= (Period - 1);
        return Math.Sqrt(x);
    }
}
