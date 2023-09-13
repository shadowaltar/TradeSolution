namespace TradeLogicCore.Algorithms.FeeCalculation;

public interface ITransactionFeeLogic
{
    abstract decimal ApplyFee(AlgoEntry current);
}
