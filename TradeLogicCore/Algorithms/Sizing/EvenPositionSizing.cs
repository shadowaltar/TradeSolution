using Autofac.Core;
using TradeCommon.Essentials.Algorithms;
using TradeCommon.Essentials.Instruments;
using TradeLogicCore.Services;

namespace TradeLogicCore.Algorithms.Sizing;
public class EvenPositionSizing : IPositionSizingAlgoLogic
{
    private readonly IPortfolioService _portfolioService;
    private readonly Context _context;

    public EvenPositionSizing(Context context)
    {
        _context = context;
    }

    public decimal GetAvailableNewPositionQuantity(Security security, int maxConcurrentPositionCount)
    {
        var positions = _portfolioService.GetAssets();
        var assetPosition = _portfolioService.GetRelatedCashPosition(security);
        if (assetPosition == null)
            return 0; // the account holds no such currency / asset for trading
        var freeBalance = assetPosition.Quantity;

        var availableNewPositionCount = Math.Max(maxConcurrentPositionCount - positions.Count, 0);

        return freeBalance > 0 ? LotRounding(security.Id, freeBalance / availableNewPositionCount) : 0;
    }

    public decimal GetSize(decimal availableCash, AlgoEntry current, AlgoEntry? last, decimal price, DateTime time)
    {
        throw new NotImplementedException();
    }

    private decimal LotRounding(int securityId, decimal proposedSize)
    {
        throw new NotImplementedException();
    }
}
