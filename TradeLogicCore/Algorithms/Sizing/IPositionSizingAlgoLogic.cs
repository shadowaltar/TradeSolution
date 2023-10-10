using TradeCommon.Essentials.Algorithms;

namespace TradeLogicCore.Algorithms.Sizing;
public interface IPositionSizingAlgoLogic
{
    abstract decimal GetSize(decimal availableCash, AlgoEntry current, AlgoEntry? last, decimal price, DateTime time);
}