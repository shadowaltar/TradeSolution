namespace TradeLogicCore.Algorithms.FeeCalculation;

public interface IUpfrontFeeLogic<T> where T : IAlgorithmVariables
{
    abstract decimal ApplyFee(AlgoEntry<T> current);
}
