using TradeCommon.Essentials.Corporates;

namespace TradeCommon.Essentials.Prices;
public record PricesAndCorporateActions(List<OhlcPrice> Prices, List<IStockCorporateAction> CorporateActions);