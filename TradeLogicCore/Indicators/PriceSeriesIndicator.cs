using TradeCommon.Essentials.Quotes;

namespace TradeLogicCore.Indicators;

public abstract class PriceSeriesIndicator<T>
{
    protected static readonly Dictionary<PriceElementType, Func<OhlcPrice, decimal>> _priceElementSelectors = new()
    {
        { PriceElementType.Open, new(p => p.O) },
        { PriceElementType.High, new(p => p.H) },
        { PriceElementType.Low, new(p => p.L) },
        { PriceElementType.Close, new(p => p.C) },
        { PriceElementType.Volume, new(p => p.V) },
        { PriceElementType.Typical3, new(p => (p.H + p.L + p.C) / 3m) },
        { PriceElementType.Typical4, new(p => (p.O + p.H + p.L + p.C) / 4m) }
    };

    /// <summary>
    /// Lookback period.
    /// </summary>
    public virtual int Period { get; set; }

    /// <summary>
    /// If true, calculate even the count of given price points is less than the <see cref="Period"/> value.
    /// </summary>
    public bool CalculateFromBeginning { get; }

    /// <summary>
    /// Selector to pick a property value from <see cref="OhlcPrice"/>. By default it is the <see cref="OhlcPrice.C"/> price.
    /// </summary>
    public virtual Func<OhlcPrice, decimal> ElementSelector { get; protected set; }

    /// <summary>
    /// Initialize this indicator.
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

    protected virtual Func<OhlcPrice, decimal> GetPriceElementSelector(PriceElementType priceElementType)
    {
        return _priceElementSelectors.TryGetValue(priceElementType, out var func) ? func : _priceElementSelectors[PriceElementType.Close];
    }

    public abstract T Calculate(IList<OhlcPrice> ohlcPrices, IList<object>? otherInputs = null);

    protected virtual bool TryGetStartIndex(IList<OhlcPrice> ohlcPrices, out int startIndex)
    {
        startIndex = ohlcPrices.Count - Period;
        startIndex = CalculateFromBeginning ? 0 : startIndex;
        return startIndex >= 0;
    }
}