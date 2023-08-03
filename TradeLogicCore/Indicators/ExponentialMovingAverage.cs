using TradeCommon.Essentials.Quotes;

namespace TradeLogicCore.Indicators;

public class ExponentialMovingAverage : PriceSeriesIndicator<double>
{
    private int _period;

    public ExponentialMovingAverage(int period, PriceElementType elementToUse = PriceElementType.Close, bool calculateFromBeginning = false) : base(period, elementToUse, calculateFromBeginning)
    {
        OlderPointCount = 1;
    }

    /// <summary>
    /// <inheritdoc/>
    /// </summary>
    public override int Period
    {
        get => _period;
        set
        {
            _period = value;
            SmoothingFactor = 2d / (1 + _period);
        }
    }

    public double SmoothingFactor { get; private set; }

    private double _previous = double.NaN;

    public override double Calculate(IList<OhlcPrice> ohlcPrices, IList<object>? otherInputs = null)
    {
        var last = ohlcPrices[^1];

        throw new NotImplementedException();
        //if (double.IsNaN(_previous))

            
        //var current = decimal.ToDouble(ElementSelector(last)) * SmoothingFactor + _previous * (1 - SmoothingFactor);

        //_previous = current;
        //return current;
    }

    public override double Calculate(IList<double> values, IList<object>? otherInputs = null)
    {
        var start = values.Count - Period;
        if (start < 0)
            return double.NaN;

        var sum = 0d;
        for (int i = Period - 1; i >= start; i--)
        {
            sum += values[i];
        }
        return sum;
    }
}
