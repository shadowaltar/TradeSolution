namespace TradeLogicCore.Algorithms.FeeCalculation;

public class OpenPositionPercentageFeeLogic<T> : ITransactionFeeLogic<T> where T : IAlgorithmVariables
{
    private decimal _percentageOfQuantity;

    public decimal PercentageOfQuantity
    {
        get => _percentageOfQuantity;
        set
        {
            if (value >= 1)
                throw new ArgumentException("The percentage must not be larger than or equals to 1.");
            _percentageOfQuantity = value;
        }
    }

    public decimal ApplyFee(AlgoEntry<T> current)
    {
        var fee = current.Quantity * PercentageOfQuantity;
        current.Quantity -= fee;
        current.Fee += fee;
        return fee;
    }
}