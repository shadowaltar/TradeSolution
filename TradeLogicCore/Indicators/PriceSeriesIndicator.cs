using TradeCommon.Essentials.Quotes;
using TradeCommon.Utils.Evaluation;

namespace TradeLogicCore.Indicators;

public abstract class PriceSeriesIndicator<T> : IIndicator
{
    /// <summary>
    /// Label of the component.
    /// </summary>
    public string Label { get; set; }

    /// <summary>
    /// Lookback period.
    /// </summary>
    public virtual int Period { get; set; }

    /// <summary>
    /// Specify the additional data point count older than <see cref="Period"/>.
    /// For example, return value will needs 1 data point older than look back period.
    /// </summary>
    public virtual int OlderPointCount { get; set; }

    /// <summary>
    /// If true, calculate even the count of given price points is less than the <see cref="Period"/> value.
    /// </summary>
    public bool CalculateFromBeginning { get; }

    /// <summary>
    /// Selector to pick a property value from <see cref="OhlcPrice"/>. By default it is the <see cref="OhlcPrice.C"/> price.
    /// </summary>
    public virtual Func<OhlcPrice, decimal> ElementSelector { get; protected set; }

    /// <summary>
    /// Subscribe this indicator.
    /// </summary>
    /// <param name="period">The interested time range / period.</param>
    /// <param name="elementToUse">The property of <see cref="OhlcPrice"/> to be used.</param>
    /// <param name="calculateFromBeginning">Whether always calculate from the beginning of the provided price points.</param>
    public PriceSeriesIndicator(int period,
                                PriceElementType elementToUse = PriceElementType.Close,
                                bool calculateFromBeginning = false)
    {
        Period = period;
        CalculateFromBeginning = calculateFromBeginning;
        ElementSelector = GetPriceElementSelector(elementToUse);
    }

    public PriceSeriesIndicator(int period): base()
    {
        Period = period;
    }

    public virtual double Next(double value)
    {
        return double.NaN;
    }

    public virtual decimal Next(decimal value)
    {
        return decimal.MinValue;
    }

    protected virtual Func<OhlcPrice, decimal> GetPriceElementSelector(PriceElementType priceElementType)
    {
        return OhlcPrice.PriceElementSelectors.TryGetValue(priceElementType, out var func) ? func : OhlcPrice.PriceElementSelectors[PriceElementType.Close];
    }


    public virtual T Calculate(IList<OhlcPrice> ohlcPrices, IList<object>? otherInputs = null)
    {
        return Calculate(ohlcPrices.Select(p => decimal.ToDouble(ElementSelector(p))).ToList(), otherInputs);
    }

    public virtual T Calculate(IList<double> values, IList<object>? otherInputs = null)
    {
        return default(T);
    }

    protected virtual bool TryGetStartIndex(IList<OhlcPrice> ohlcPrices, out int startIndex)
    {
        startIndex = ohlcPrices.Count - (Period + 1);
        startIndex = CalculateFromBeginning ? 0 : startIndex;
        return startIndex >= 0;
    }

    protected virtual bool TryGetStartIndex(IList<double> values, out int startIndex)
    {
        startIndex = values.Count - (Period + 1);
        startIndex = CalculateFromBeginning ? 0 : startIndex;
        return startIndex >= 0;
    }
}