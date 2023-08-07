namespace TradeCommon.Essentials.Portfolios;

/// <summary>
/// Portfolio is an aggregation of positions including cash-position (uninvested/free cash).
/// </summary>
public record Portfolio
{
    public Portfolio(decimal initialCash)
    {
        InitialFreeCash = initialCash;
        FreeCash = initialCash;
        Notional = initialCash;
    }

    public decimal InitialFreeCash { get; set; }
    public decimal FreeCash { get; set; }
    public decimal Notional { get; set; }
    public decimal TotalRealizedPnl { get; set; }
}
