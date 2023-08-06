using Common;

namespace TradeCommon.Calculations;
public class SimpleMovingAverage : Calculator
{
    private double _previous = double.NaN;
    private decimal _previousDecimal = decimal.MinValue;

    private readonly LinkedList<double> _cachedValues = new();
    private readonly LinkedList<decimal> _cachedDecimalValues = new();

    public SimpleMovingAverage(int period, string label = "")
    {
        Period = period;
        Label = label;
    }

    public override double Next(double value)
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

    public override decimal Next(decimal value)
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
                _previousDecimal = decimal.Divide(sum, Period);
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
            _previousDecimal = (_previousDecimal * Period + value - first) / Period;
            return _previousDecimal;
        }
    }
}
