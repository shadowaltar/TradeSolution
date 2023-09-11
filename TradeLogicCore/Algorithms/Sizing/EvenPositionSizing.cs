using TradeCommon.Essentials.Instruments;
using TradeLogicCore.Services;

namespace TradeLogicCore.Algorithms.Sizing;
public class EvenPositionSizing<T> : IPositionSizingAlgoLogic<T> where T : IAlgorithmVariables
{
    private readonly IPortfolioService _portfolioService;

    public EvenPositionSizing(IAlgorithm<T> mainAlgo)
    {
        Algorithm = mainAlgo;
        _portfolioService = Algorithm.Context.Services.Portfolio;
    }

    public IAlgorithm<T> Algorithm { get; }

    public decimal GetAvailableNewPositionQuantity(Security security, int maxConcurrentPositionCount)
    {
        var positions = _portfolioService.GetOpenPositions();
        var asset = security.EnsureCurrencyAsset();
        var assetPosition = _portfolioService.GetAsset(asset.Id);
        if (assetPosition == null)
            return 0; // the account holds no such currency / asset for trading
        var freeBalance = assetPosition.Quantity;

        var availableNewPositionCount = Math.Max(maxConcurrentPositionCount - positions.Count, 0);

        if (freeBalance > 0)
            return LotRounding(security.Id, freeBalance / availableNewPositionCount);

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
