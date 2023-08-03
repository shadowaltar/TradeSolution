using TradeLogicCore.Algorithms.EnterExit;
using TradeLogicCore.Algorithms.Screening;
using TradeLogicCore.Algorithms.Sizing;

namespace TradeLogicCore.Algorithms;
public interface IAlgorithm
{
    IPositionSizingAlgoLogic Sizing { get; }
    IEnterPositionAlgoLogic Entering { get; }
    IExitPositionAlgoLogic Exiting { get; }
    ISecuritySelectionAlgoLogic Screening { get; }

}
