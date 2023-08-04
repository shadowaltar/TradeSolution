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

public class ExponentialMovingAverageV2 : Calculator
{
    private double _previous = double.NaN;
    private decimal _previousDecimal = decimal.MinValue;

    private readonly LinkedList<double> _cachedValues = new();
    private readonly LinkedList<decimal> _cachedDecimalValues = new();

    public int Smoothing { get; }
    public double[] Factors { get; }
    public decimal[] DecimalFactors { get; }

    public ExponentialMovingAverageV2(int period, int smoothing = 2, string label = "")
    {
        if (period < 1)
            throw new ArgumentException("Period must be at least 1.");
        if (smoothing < 2)
            throw new ArgumentException("Smoothing must be at least 2.");
        Period = period;
        Label = label ?? "EMAv2";
        Smoothing = smoothing;
        Factors = new double[period];
        GenerateFactors();
    }

    private void GenerateFactors()
    {
        // = kp(t) + k(1-k)p(t-1) + k(1-k)^2*p(t-2) + k(1-k)^3*p(t-3) + ... + k(1-k)^n*p(t-n)
        var a = Smoothing / (1d + Period);
        var residual = 1d;
        var decimalResidual = 1m;
        for (int i = 0; i < Period - 1; i++)
        {
            var coeff = a * Math.Pow(1 - a, i);
            var decimalCoeff = Convert.ToDecimal(coeff);
            residual = 1 - coeff;
            decimalResidual = 1 - decimalCoeff;
            Factors[i] = coeff;
            DecimalFactors[i] = Convert.ToDecimal(coeff);
        }
        Factors[Factors.Length - 1] = residual;
        DecimalFactors[DecimalFactors.Length - 1] = decimalResidual;
    }

    public override double Next(double value)
    {
        _cachedValues.AddLast(value);
        if (_cachedValues.Count != Period)
        {
            return double.NaN;
        }
        var sum = 0d;
        var i = 0;
        foreach (var item in _cachedValues)
        {
            sum += (Factors[i] * item);
            i++;
        }
        _previous = sum;
        _cachedValues.RemoveFirst();
        return _previous;
    }

    public override decimal Next(decimal value)
    {
        _cachedDecimalValues.AddLast(value);
        if (_cachedDecimalValues.Count != Period)
        {
            return decimal.MinValue;
        }
        var sum = 0m;
        var i = 0;
        foreach (var item in _cachedDecimalValues)
        {
            sum += (DecimalFactors[i] * item);
            i++;
        }
        _previousDecimal = sum;
        _cachedDecimalValues.RemoveFirst();
        return _previousDecimal;
    }
}
