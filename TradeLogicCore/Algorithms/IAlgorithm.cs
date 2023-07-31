using TradeLogicCore.Algorithms.Sizing;

namespace TradeLogicCore.Algorithms;
public interface IAlgorithm
{
    void Initialize(params object[] args);

    IPositionSizingLogic PositionSizingLogic { get; }

    TimeSpan SecurityPoolUpdateFrequency { get; }

    TimeSpan PositionRevisionFrequency { get; }

    void DecideSecurityPool();

    void OpenPosition();

    void ClosePosition();
}
