using Common;

namespace TradeCommon.Calculations;
public class StandardDeviation : Calculator
{
    private double _previousAverage = double.NaN;
    private decimal _previousDecimalAverage = decimal.MinValue;

    private readonly LinkedList<double> _cachedValues = new();
    private readonly LinkedList<decimal> _cachedDecimalValues = new();

    public StandardDeviation(int period, string label = "")
    {
        Period = period;
        Label = label;
    }

    public override double Next(double value)
    {
        _cachedValues.AddLast(value);
        if (_previousAverage.IsNaN())
        {
            if (_cachedValues.Count == Period)
            {
                var sum = 0d;
                foreach (var item in _cachedValues)
                {
                    sum += item;
                }
                _previousAverage = sum / Period;

                return FindStdev(_cachedValues, _previousAverage);
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
            _previousAverage = ((_previousAverage * Period) + value - first) / Period;

            return FindStdev(_cachedValues, _previousAverage);
        }
    }

    public override decimal Next(decimal value)
    {
        _cachedDecimalValues.AddLast(value);
        if (_previousDecimalAverage == decimal.MinValue)
        {
            if (_cachedDecimalValues.Count == Period)
            {
                var sum = 0m;
                foreach (var item in _cachedDecimalValues)
                {
                    sum += item;
                }
                _previousDecimalAverage = decimal.Divide(sum, Period);

                return FindStdev(_cachedDecimalValues, _previousDecimalAverage);
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
            _previousDecimalAverage = ((_previousDecimalAverage * Period) + value - first) / Period;

            return FindStdev(_cachedDecimalValues, _previousDecimalAverage);
        }
    }

    /// <summary>
    /// Calculate using the last-n items in the given <paramref name="values"/>; n = <see cref="Calculator.Period"/>.
    /// </summary>
    /// <param name="values">The values to be calculated. Only the last-n items will be used.</param>
    /// <returns>Returns the sample-standard-deviation. If <paramref name="values"/> item count < <see cref="Calculator.Period"/>, returns NaN.</returns>
    public double Calculate(IList<double> values)
    {
        if (values.Count < Period)
            return double.NaN;
        var sum = 0d;
        for (int i = Period - 1; i < values.Count; i++)
        {
            double item = values[i];
            sum += item;
        }
        var x = sum / Period;
        return FindStdev(values, x);
    }

    private double FindStdev(ICollection<double> _cachedValues, double average)
    {
        var squaredSum = 0d;
        foreach (var item in _cachedValues)
        {
            squaredSum += (item - average) * (item - average);
        }
        return Math.Sqrt(squaredSum / (Period - 1));
    }

    private decimal FindStdev(ICollection<decimal> _cachedValues, decimal average)
    {
        var squaredSum = 0m;
        foreach (var item in _cachedValues)
        {
            squaredSum += (item - average) * (item - average);
        }
        var inDouble = squaredSum.ToDouble();
        return (decimal)Math.Sqrt(inDouble / (Period - 1));
    }
}
