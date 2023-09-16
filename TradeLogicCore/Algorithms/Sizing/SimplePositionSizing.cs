using TradeCommon.Essentials.Algorithms;
using TradeLogicCore.Algorithms.Sizing;

namespace TradeLogicCore.Algorithms;

public class SimplePositionSizing : IPositionSizingAlgoLogic
{
    public decimal GetSize(decimal availableCash, AlgoEntry current, AlgoEntry last, decimal price, DateTime time)
    {
        // a simple one which invest all and without lot-size rounding
        var setAside = 0m;
        var quantity = (availableCash - setAside) / price;
        return current.Security.RoundLotSize(quantity);
    }
}