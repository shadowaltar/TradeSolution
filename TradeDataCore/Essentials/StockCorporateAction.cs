using System.Diagnostics;

namespace TradeDataCore.Essentials;

public interface IStockCorporateAction
{
    DateTime SettlementDate { get; }
}

/// <summary>
/// A CorporateAction which a payment to shareholders
/// that is made in additional shares.
/// </summary>
public record StockDividendEvent(DateTime ExDate, DateTime PaymentDate, decimal Amount) : IStockCorporateAction
{
    public DateTime SettlementDate => PaymentDate;
}

/// <summary>
/// A CorporateAction which stands for one or more shares
/// to be split (or reverse split / merge) into one or more shares.
/// </summary>
public record StockSplitEvent(DateTime PayableDate, DateTime ExDate, int Numerator, int Denominator) : IStockCorporateAction
{
    public DateTime SettlementDate => ExDate;
    public double Ratio => Numerator / (double)Denominator;
}
