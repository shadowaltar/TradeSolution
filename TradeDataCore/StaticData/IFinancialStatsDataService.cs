using TradeCommon.Essentials.Corporates;
using TradeCommon.Essentials.Fundamentals;
using TradeCommon.Essentials.Instruments;

namespace TradeDataCore.StaticData;

public interface IFinancialStatsDataService
{
    List<IStockCorporateAction> GetCorporateActions(Security security, DateTime start, DateTime end);
    List<FinancialStats> GetFundamentals(Security security, DateTime start, DateTime end);
}