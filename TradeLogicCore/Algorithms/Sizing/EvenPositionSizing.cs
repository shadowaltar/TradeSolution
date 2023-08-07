using TradeCommon.Essentials.Portfolios;
using TradeDataCore.Instruments;
using TradeLogicCore.Services;

namespace TradeLogicCore.Algorithms.Sizing;
public class EvenPositionSizing<T> : IPositionSizingAlgoLogic<T> where T : IAlgorithmVariables
{
    private readonly IPortfolioService _portfolioService;
    private readonly ISecurityService _securityService;

    public EvenPositionSizing(IPortfolioService portfolioService,
        ISecurityService securityService)
    {
        _portfolioService = portfolioService;
        _securityService = securityService;
    }

    public decimal GetAvailableNewPositionQuantity(int securityId, int maxConcurrentPositionCount)
    {
        var positions = _portfolioService.GetOpenPositions();
        var freeBalance = _portfolioService.RemainingBalance;

        var availableNewPositionCount = Math.Max(maxConcurrentPositionCount - positions.Count, 0);

        if (freeBalance > 0)
            return LotRounding(securityId, freeBalance / availableNewPositionCount);

        return 0;
    }

    public decimal GetSize(decimal availableCash, AlgoEntry<T> current, AlgoEntry<T> last, decimal price, DateTime time)
    {
        throw new NotImplementedException();
    }

    private decimal LotRounding(int securityId, decimal proposedSize)
    {
        throw new NotImplementedException();
    }
}
