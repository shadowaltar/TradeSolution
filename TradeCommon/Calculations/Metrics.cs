using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TradeCommon.Calculations;
public class Metrics
{
    public static double GetAnnualizedReturn(double start, double end, DateTime startTime, DateTime endTime)
    {
        var years = 365 / (endTime - startTime).TotalDays;
        return Math.Pow(((end - start) / start + 1), years) - 1;
    }
}
