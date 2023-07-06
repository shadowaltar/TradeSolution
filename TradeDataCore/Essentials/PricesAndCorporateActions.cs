namespace TradeDataCore.Essentials;
public record PricesAndCorporateActions(List<OhlcPrice> Prices, List<IStockCorporateAction> CorporateActions);