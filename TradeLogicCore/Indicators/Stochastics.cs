using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TradeDataCore.Essentials;

namespace TradeLogicCore.Indicators;
internal class Stochastics : PriceSeriesIndicator<(double, double)>
{
    public int KPeriod { get; }
    public int DPeriod { get; }

    public Stochastics(int kPeriod = 14, int dPeriod = 7) : base(Math.Max(kPeriod, dPeriod))
    {
        KPeriod = kPeriod;
        DPeriod = dPeriod;
    }

    public override (double, double) Calculate(IList<OhlcPrice> ohlcPrices, IList<object>? otherInputs = null)
    {
        var kStart = ohlcPrices.Count - KPeriod;
        var dStart = ohlcPrices.Count - DPeriod;
        if (kStart < 0 || dStart < 0)
            return (double.NaN, double.NaN);

        var start = Math.Min(kStart, dStart);
        var kLow = ohlcPrices[Period - 1].Low;
        var kHigh = ohlcPrices[Period - 1].High;
        var dLow = ohlcPrices[Period - 1].Low;
        var dHigh = ohlcPrices[Period - 1].High;
        for (int i = Period - 1; i >= start; i--)
        {
            if (i >= kStart)
            {
                kLow = Math.Min(ohlcPrices[i].Low, kLow);
                kHigh = Math.Max(ohlcPrices[i].High, kHigh);
            }
            if (i >= dStart)
            {
                dLow = Math.Min(ohlcPrices[i].Low, dLow);
                dHigh = Math.Max(ohlcPrices[i].High, dHigh);
            }
        }
        static decimal GetResult(decimal recentClose, decimal high, decimal low)
        {
            return (recentClose - low) / (high - low) * 100;
        }
        var close = ohlcPrices[Period - 1].Close;
        return (decimal.ToDouble(GetResult(close, kHigh, kLow)), decimal.ToDouble(GetResult(close, dHigh, dLow)));
    }
}
