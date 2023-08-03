using Common;

namespace TradeCommon.Calculations;

public class ExponentialMovingAverage : Calculator
{
    private double _previous = double.NaN;
    private decimal _previousDecimal = decimal.MinValue;

    private readonly LinkedList<double> _cachedValues = new();
    private readonly LinkedList<decimal> _cachedDecimalValues = new();

    private double _factor = 0;
    private decimal _factorDecimal = 0;

    public int Smoothing { get; }

    public ExponentialMovingAverage(int period, int smoothing = 2, string label = "")
    {
        Period = period;
        Label = label;
        Smoothing = smoothing;

        _factor = Smoothing / (double)(1 + Period);
        _factorDecimal = Smoothing / (decimal)(1 + Period);
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
            _cachedValues.RemoveFirst();
            _previous = value * _factor +_previous * (1 - _factor);
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
            _cachedDecimalValues.RemoveFirst();
            _previousDecimal = value * _factorDecimal + _previousDecimal * (1 - _factorDecimal);
            return _previousDecimal;
        }
    }
}
