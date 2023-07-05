namespace TradeDataCore.Essentials;

public class StockCorporateAction
{
    public DateTime EventDate { get; set; }
}

/// <summary>
/// A CorporateAction which a payment to shareholders
/// that is made in additional shares.
/// </summary>
public class StockDividendEvent : StockCorporateAction
{
    public decimal DividendAmount { get; set; }
}

/// <summary>
/// A CorporateAction which stands for one or more shares
/// to be split (or reverse split) into one or more shares.
/// </summary>
public class StockSplitEvent : StockCorporateAction
{
    public int Numerator { get; set; }
    public int Denominator { get; set; }
    public double Ratio => Numerator / (double)Denominator;
}
