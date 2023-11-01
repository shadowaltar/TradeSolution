using Common;

namespace TradeCommon.Calculations;

public class ExponentialMovingAverage : Calculator
{
    private double _previous = double.NaN;
    private decimal _previousDecimal = decimal.MinValue;

    private List<double>? _cachedValues;
    private List<decimal>? _cachedDecimalValues;

    private readonly double _factor = 0;
    private readonly decimal _factorDecimal = 0;

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
        if (_previous.IsNaN())
        {
            _cachedValues ??= new();
            _cachedValues.Add(value);
            if (_cachedValues.Count == Period)
            {
                var sum = 0d;
                foreach (var item in _cachedValues)
                {
                    sum += item;
                }
                _previous = sum / Period;
                _cachedValues = null;
                return _previous;
            }
            else
            {
                return double.NaN;
            }
        }
        else
        {
            _previous = (value * _factor) + (_previous * (1 - _factor));
            return _previous;
        }
    }

    public override decimal Next(decimal value)
    {
        if (!_previousDecimal.IsValid())
        {
            _cachedDecimalValues ??= new();
            _cachedDecimalValues.Add(value);
            if (_cachedDecimalValues.Count == Period)
            {
                var sum = 0m;
                foreach (var item in _cachedDecimalValues)
                {
                    sum += item;
                }
                _previousDecimal = decimal.Divide(sum, Period);
                _cachedDecimalValues = null;
                return _previousDecimal;
            }
            else
            {
                return decimal.MinValue;
            }
        }
        else
        {
            _previousDecimal = (value * _factorDecimal) + (_previousDecimal * (1 - _factorDecimal));
            return _previousDecimal;
        }
    }
}
