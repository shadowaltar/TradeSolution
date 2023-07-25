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
        OlderPointCount = 1; // Stdev is usually calculated against return, not the data point itself.
    }

    public override double Calculate(IList<double> values, IList<object>? otherInputs = null)
    {
        if (!TryGetStartIndex(values, out var startIndex)) return double.NaN;

        var previous = values[startIndex];

        var returns = new List<double>();


        for (int i = startIndex + 1; i < values.Count; i++)
        {
            returns.Add((values[i] - previous) / previous);
            previous = values[i];
        }

        var sum = 0d;
        for (int i = 0; i < returns.Count; i++)
        {
            sum += returns[i];
        }
        var x = 0d;
        var averageReturn = sum / Period;
        for (int i = 0; i < returns.Count; i++)
        {
            x += Math.Pow(returns[i] - averageReturn, 2);
        }

        if (_isPopulation)
            x /= Period;
        else
            x /= (Period - 1);
        return Math.Sqrt(x);
    }
}
