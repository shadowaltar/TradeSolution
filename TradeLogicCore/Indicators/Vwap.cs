using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TradeCommon.Essentials.Quotes;

namespace TradeLogicCore.Indicators;

/// <summary>
/// Calculate a VWAP value.
/// Default is for a stock: period is by default infinite (all points in the interested time range),
/// and starting point is the start of whole series.
/// </summary>
public class Vwap : PriceSeriesIndicator<decimal>
{
    /// <summary>
    /// <inheritdoc/>
    /// </summary>
    /// <param name="period"></param>
    /// <param name="elementToUse"></param>
    /// <param name="calculateFromBeginning"></param>
    public Vwap(int period = int.MaxValue, PriceElementType elementToUse = PriceElementType.Close, bool calculateFromBeginning = true)
        : base(period, elementToUse, calculateFromBeginning)
    {
    }

    public override decimal Calculate(IList<OhlcPrice> ohlcPrices, IList<object>? otherInputs = null)
    {
        if (!TryGetStartIndex(ohlcPrices, out var startIndex)) return decimal.MinValue;

        var productSum = 0m;
        var totalVolume = 0m;
        for (var i = startIndex; i < ohlcPrices.Count; i++)
        {
            var price = ohlcPrices[i];
            productSum += ElementSelector(price) * price.V;
            totalVolume += price.V;
        }
        return productSum / totalVolume;
    }
}
