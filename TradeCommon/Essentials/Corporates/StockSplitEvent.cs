namespace TradeCommon.Essentials.Corporates;

/// <summary>
/// A CorporateAction which stands for one or more shares
/// to be split (or reverse split / merge) into one or more shares.
/// </summary>
public record StockSplitEvent(DateTime PayableDate, DateTime ExDate, int Numerator, int Denominator) : IStockCorporateAction
{
    public DateTime SettlementDate => ExDate;
    public double Ratio => Numerator / (double)Denominator;
}
