namespace TradeCommon.Essentials.Quotes;

/// <summary>
/// A candlestick price object.
/// </summary>
/// <param name="O">Open</param>
/// <param name="H">High</param>
/// <param name="L">Low</param>
/// <param name="C">Close</param>
/// <param name="AC">Adjusted Close</param>
/// <param name="V">Volume</param>
/// <param name="T">StartTime</param>
public record OhlcPrice()
{
    public decimal O { get; set; }
    public decimal H { get; set; }
    public decimal L { get; set; }
    public decimal C { get; set; }
    public decimal AC { get; set; }
    public decimal V { get; set; }
    public DateTime T { get; set; }

    public OhlcPrice(decimal o, decimal h, decimal l, decimal c, decimal ac, decimal v, DateTime t) : this()
    {
        O = o;
        H = h;
        L = l;
        C = c;
        AC = ac;
        V = v;
        T = t;
    }

    public OhlcPrice(decimal o, decimal h, decimal l, decimal c, decimal v, DateTime t) : this(o, h, l, c, c, v, t) { }

    public static implicit operator OhlcPrice?((decimal O, decimal H, decimal L, decimal C, decimal V, DateTime T) tuple)
        => new(tuple.O, tuple.H, tuple.L, tuple.C, tuple.C, tuple.V, tuple.T);

    public static implicit operator OhlcPrice?((decimal O, decimal H, decimal L, decimal C, decimal AC, decimal V, DateTime T) tuple)
        => new(tuple.O, tuple.H, tuple.L, tuple.C, tuple.AC, tuple.V, tuple.T);

    public static readonly IReadOnlyDictionary<PriceElementType, Func<OhlcPrice, decimal>> PriceElementSelectors = new Dictionary<PriceElementType, Func<OhlcPrice, decimal>>
    {
        { PriceElementType.Open, new(p => p.O) },
        { PriceElementType.High, new(p => p.H) },
        { PriceElementType.Low, new(p => p.L) },
        { PriceElementType.Close, new(p => p.C) },
        { PriceElementType.AdjClose, new(p => p.AC) },
        { PriceElementType.Volume, new(p => p.V) },
        { PriceElementType.Typical3, new(p => (p.H + p.L + p.C) / 3m) },
        { PriceElementType.Typical4, new(p => (p.O + p.H + p.L + p.C) / 4m) }
    };

    public override string? ToString()
    {
        return $"[{T:yyyyMMdd-HHmmss}] {O:G29}|{H:G29}|{L:G29}|{C:G29}, {V:G29}";
    }
}
