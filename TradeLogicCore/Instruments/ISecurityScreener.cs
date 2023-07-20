using TradeCommon.Essentials.Instruments;

namespace TradeLogicCore.Instruments;

public interface ISecurityScreener
{
    public List<Security> Filter(DateTime asOfTime, SecurityScreeningCriteria criteriaType)
    {
        throw new NotImplementedException();
    }
}

[Flags]
public enum SecurityScreeningCriteria
{
    DerivedByOhlcPrices,
    Fundamentals,
}