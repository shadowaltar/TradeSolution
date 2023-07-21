using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TradeCommon.Utils.Evaluation;
public static class BinaryFunctions
{
    public static decimal BinomialCoefficient(decimal n, decimal k)
    {
        var product = 1m;
        for (decimal i = 1; i <= k; i++)
        {
            product *= (n - i + 1) / i;
        }
        return product;
    }

    public static double BinomialCoefficient(double n, double k)
    {
        var product = 1d;
        for (double i = 1; i <= k; i++)
        {
            product *= (n - i + 1) / i;
        }
        return product;
    }
}
