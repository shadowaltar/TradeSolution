using TradeDataCore.Essentials;

namespace TradeLogicCore.Indicators;

internal interface IPriceSeriesIndicator<T>
{
    T Calculate(IList<OhlcPrice> ohlcPrices, IList<object>? otherInputs = null);



    protected static readonly Func<OhlcPrice, decimal> _defaultSelector = new(p => p.Close);
}

internal abstract class PriceSeriesIndicator<T> : IPriceSeriesIndicator<T>
{
    protected readonly Func<OhlcPrice, decimal> _selector;

    public int Period { get; }

    public PriceSeriesIndicator(int period, Func<OhlcPrice, decimal>? selector = null)
    {
        Period = period;
        _selector = selector ?? IPriceSeriesIndicator<double>._defaultSelector;
    }

    public abstract T Calculate(IList<OhlcPrice> ohlcPrices, IList<object>? otherInputs = null);
}