using TradeCommon.Constants;

namespace TradeLogicCore.Instruments;

public interface IStockScreener
{
    Task<SecurityScreeningResult> Filter(ExchangeType exchange, ScreeningCriteria criteria);
}