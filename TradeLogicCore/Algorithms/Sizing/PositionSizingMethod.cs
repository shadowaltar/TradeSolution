namespace TradeLogicCore.Algorithms;

public enum PositionSizingMethod
{
    Unknown,
    Zero,
    Fixed,
    All,
    PreserveFixed,// always preserve fixed amount
    AsSmallAsPossible,
}