using TradeCommon.Essentials.Corporates;
using TradeCommon.Essentials.Quotes;

namespace TradeCommon.Essentials.Prices;
public record PricesAndCorporateActions(List<OhlcPrice> Prices, List<IStockCorporateAction> CorporateActions);