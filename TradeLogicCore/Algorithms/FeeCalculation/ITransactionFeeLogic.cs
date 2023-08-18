namespace TradeLogicCore.Algorithms.FeeCalculation;

public interface ITransactionFeeLogic<T> where T : IAlgorithmVariables
{
    abstract decimal ApplyFee(AlgoEntry<T> current);
}
