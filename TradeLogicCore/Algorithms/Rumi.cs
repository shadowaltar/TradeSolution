using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TradeLogicCore.Algorithms.Sizing;
using TradeLogicCore.Services;

namespace TradeLogicCore.Algorithms;
public class Rumi : IAlgorithm
{
    private readonly IPortfolioService _portfolioService;

    public Rumi(IPortfolioService portfolioService, EvenPositionSizingLogic evenPositionSizingLogic)
    {
        _portfolioService = portfolioService;
        PositionSizingLogic = evenPositionSizingLogic;
    }

    public void Initialize(params object[] args)
    {
        throw new NotImplementedException();
    }

    public TimeSpan SecurityPoolUpdateFrequency => throw new NotImplementedException();

    public TimeSpan PositionRevisionFrequency => throw new NotImplementedException();

    public IPositionSizingLogic PositionSizingLogic { get; }

    public void ClosePosition()
    {
        throw new NotImplementedException();
    }

    public void DecideSecurityPool()
    {
        throw new NotImplementedException();
    }

    public void OpenPosition()
    {
        throw new NotImplementedException();
    }
}
