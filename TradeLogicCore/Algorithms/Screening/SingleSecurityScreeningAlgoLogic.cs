using TradeCommon.Essentials.Instruments;

namespace TradeLogicCore.Algorithms.Screening;

public class SingleSecurityScreeningAlgoLogic<T> : ISecurityScreeningAlgoLogic<T>
{
    private static readonly List<Security> _empty = new List<Security>();

    public IReadOnlyList<Security> Pick(List<Security> securityPool)
    {
        if (securityPool == null) return _empty;
        if (securityPool.Count == 1) return securityPool;
        return new List<Security> { securityPool.First() };
    }
}
