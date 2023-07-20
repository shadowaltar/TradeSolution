using TradeCommon.Essentials.Quotes;

namespace TradeLogicCore.Indicators;
public class Stochastics : PriceSeriesIndicator<decimal[]>
{
    public int[] Periods { get; }
    public int KPeriod { get; }
    public int DPeriod { get; }
    public int JPeriod { get; }

    private readonly List<StochasticsComponent> _components = new();

    public Stochastics(PriceElementType elementToUse = PriceElementType.Close, bool calculateFromBeginning = false, params int[] periods)
        : base(periods.Max(), elementToUse, calculateFromBeginning)
    {
        if (periods.Length == 0) throw new ArgumentException(nameof(periods));
        KPeriod = periods[0];
        _components.Add(new StochasticsComponent(periods[0], "K"));

        if (periods.Length > 1)
        {
            DPeriod = periods[1];
            _components.Add(new StochasticsComponent(periods[1], "D"));
        }
        if (periods.Length > 2)
        {
            JPeriod = periods[2];
            _components.Add(new StochasticsComponent(periods[2], "J"));
        }
    }

    public override decimal[] Calculate(IList<OhlcPrice> ohlcPrices, IList<object>? otherInputs = null)
    {
        var results = new decimal[_components.Count];
        for (int i = 0; i < _components.Count; i++)
        {
            var component = _components[i];
            var result = component.Calculate(ohlcPrices, otherInputs);
            if (result == decimal.MinValue)
                continue;
            results[i] = result;
        }
        return results;
    }

    public class StochasticsComponent : PriceSeriesIndicator<decimal>
    {
        /// <summary>
        /// Label of the component.
        /// </summary>
        public string Label { get; set; }

        public StochasticsComponent(int period, string label) : base(period)
        {
            Period = period;
            Label = label;
        }

        public override decimal Calculate(IList<OhlcPrice> ohlcPrices, IList<object>? otherInputs = null)
        {
            if (!TryGetStartIndex(ohlcPrices, out var startIndex)) return decimal.MinValue;

            var low = decimal.MaxValue;
            var high = decimal.MinValue;
            var close = ohlcPrices[ohlcPrices.Count - 1].C;

            for (int j = startIndex; j < ohlcPrices.Count; j++)
            {
                var p = ohlcPrices[j];
                if (low > p.L)
                    low = p.L;
                if (high < p.H)
                    high = p.H;
            }
            return RunFormula(close, high, low);
        }

        private static decimal RunFormula(decimal c, decimal h, decimal l) => (c - l) / (h - l) * 100;
    }
}
