using TradeCommon.Essentials.Corporates;
using TradeCommon.Essentials.Fundamentals;
using TradeCommon.Essentials.Instruments;

namespace TradeDataCore.StaticData;
public class FinancialStatsDataService : IFinancialStatsDataService
{
    public List<IStockCorporateAction> GetCorporateActions(Security security, DateTime start, DateTime end)
    {
        throw new NotImplementedException();
    }

    public List<FinancialStats> GetFundamentals(Security security, DateTime start, DateTime end)
    {
        throw new NotImplementedException();
    }
}
