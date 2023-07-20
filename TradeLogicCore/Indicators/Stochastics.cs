using TradeCommon.Essentials.Prices;

namespace TradeLogicCore.Indicators;
public class Stochastics : PriceSeriesIndicator<decimal[]>
{
    public int[] Periods { get; }
    public int KPeriod { get; }
    public int DPeriod { get; }
    public int JPeriod { get; }

    private readonly List<Component> _components = new();

    public class Component
    {
        public int Period { get; set; }
        public string Label { get; set; }
        public Component(int period, string label)
        {
            Period = period;
            Label = label;
        }
    }

    public Stochastics(params int[] periods) : base(periods.Max())
    {
        if (periods.Length == 0) throw new ArgumentException(nameof(periods));
        KPeriod = periods[0];
        _components.Add(new Component(periods[0], "K"));

        if (periods.Length > 1)
        {
            DPeriod = periods[1];
            _components.Add(new Component(periods[1], "D"));
        }
        if (periods.Length > 2)
        {
            JPeriod = periods[2];
            _components.Add(new Component(periods[2], "J"));
        }
    }

    public override decimal[] Calculate(IList<OhlcPrice> ohlcPrices, IList<object>? otherInputs = null)
    {
        var results = new decimal[_components.Count];
        for (int i = 0; i < _components.Count; i++)
        {
            var component = _components[i];
            var startIndex = ohlcPrices.Count - component.Period;
            if (startIndex < 0)
                continue;

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
            results[i] = RunFormula(close, high, low);
        }
        return results;
    }

    static decimal RunFormula(decimal c, decimal h, decimal l) => (c - l) / (h - l) * 100;
}
