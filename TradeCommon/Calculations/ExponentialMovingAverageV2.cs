﻿namespace TradeCommon.Calculations;

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
        DecimalFactors = new decimal[period];
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
            residual -= coeff;
            decimalResidual -= decimalCoeff;
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
            sum += Factors[i] * item;
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
        var last = _cachedDecimalValues.Last;
        if (Period == 1)
        {
            _previousDecimal = last!.Value;
            _cachedDecimalValues.RemoveFirst();
            return _previousDecimal;
        }
        // factors are from latest to earliest
        // cached values are from earliest to latest
        var node = _cachedDecimalValues.Last;
        for (int i = 0; i < Period; i++)
        {
            var v = node!.Value;
            sum += DecimalFactors[i] * v;
            node = node.Previous;
        }
        _previousDecimal = sum;
        _cachedDecimalValues.RemoveFirst();
        return _previousDecimal;
    }
}
