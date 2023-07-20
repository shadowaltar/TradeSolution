namespace TradeCommon.Essentials.Corporates;

/// <summary>
/// A CorporateAction which a payment to shareholders
/// that is made in additional shares.
/// </summary>
public record StockDividendEvent(DateTime ExDate, DateTime PaymentDate, decimal Amount) : IStockCorporateAction
{
    public DateTime SettlementDate => PaymentDate;
}
