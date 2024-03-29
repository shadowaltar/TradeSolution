﻿using Common;

namespace TradeCommon.Calculations;
public static class Maths
{
    public static double GetAnnualizedReturn(double start, double end, DateTime startTime, DateTime endTime)
    {
        var years = 365 / (endTime - startTime).TotalDays;
        return Math.Pow(end / start, years) - 1;
    }

    public static double GetStandardDeviation(IList<double> values, bool isSample = true, bool ignoreInvalid = true)
    {
        var sum = 0d;
        foreach (var item in values)
        {
            if (ignoreInvalid)
                if (!item.IsValid())
                    continue;
            sum += item;
        }
        var x = sum / values.Count;

        var squaredSum = 0d;
        foreach (var item in values)
        {
            squaredSum += (item - x) * (item - x);
        }
        return (double)Math.Sqrt(squaredSum / (values.Count - (isSample ? 1 : 0)));
    }

    public static decimal ZeroDivision(this decimal numerator, decimal denominator, decimal valueIfZeroDevision = 0m)
    {
        return denominator == 0 ? valueIfZeroDevision : numerator / denominator;
    }
}
