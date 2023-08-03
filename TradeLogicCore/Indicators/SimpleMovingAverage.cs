using Common;
using TradeCommon.Essentials.Quotes;

namespace TradeLogicCore.Indicators;

public class SimpleMovingAverage : PriceSeriesIndicator<double>
{
    public SimpleMovingAverage(int period, PriceElementType elementToUse = PriceElementType.Close, bool calculateFromBeginning = false) : base(period, elementToUse, calculateFromBeginning)
    {
        _periodDecimal = new decimal(period);
    }

    private double _previous = double.NaN;
    private decimal _previousDecimal = decimal.MinValue;

    private readonly LinkedList<double> _cachedValues = new();
    private readonly LinkedList<decimal> _cachedDecimalValues = new();
    private readonly decimal _periodDecimal;

    public double Next(double value)
    {
        _cachedValues.AddLast(value);
        if (_previous.IsNaN())
        {
            if (_cachedValues.Count == Period)
            {
                var sum = 0d;
                foreach (var item in _cachedValues)
                {
                    sum += item;
                }
                _previous = sum / Period;
                return _previous;
            }
            else
            {
                return double.NaN;
            }
        }
        else
        {
            double first = _cachedValues.First!.ValueRef;
            _cachedValues.RemoveFirst();
            _previous = (_previous * Period + value - first) / Period;
            return _previous;
        }
    }

    public decimal Next(decimal value)
    {
        _cachedDecimalValues.AddLast(value);
        if (_previousDecimal == decimal.MinValue)
        {
            if (_cachedDecimalValues.Count == Period)
            {
                var sum = 0m;
                foreach (var item in _cachedDecimalValues)
                {
                    sum += item;
                }
                _previousDecimal = decimal.Divide(sum, _periodDecimal);
                return _previousDecimal;
            }
            else
            {
                return decimal.MinValue;
            }
        }
        else
        {
            decimal first = _cachedDecimalValues.First!.ValueRef;
            _cachedDecimalValues.RemoveFirst();
            _previousDecimal = (_previousDecimal * _periodDecimal + value - first) / _periodDecimal;
            return _previousDecimal;
        }
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