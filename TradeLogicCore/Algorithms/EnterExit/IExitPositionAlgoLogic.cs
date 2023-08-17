namespace TradeLogicCore.Algorithms.EnterExit;
public interface IExitPositionAlgoLogic<T>
{
    decimal StopLossRatio { get; }

    void Close(IAlgorithemContext<T> context, AlgoEntry<T> current, decimal exitPrice, DateTime exitTime);

    void StopLoss(IAlgorithemContext<T> context, AlgoEntry<T> current, AlgoEntry<T> last, DateTime exitTime);
}
