namespace TradeLogicCore.Algorithms.EnterExit;
public interface IExitPositionAlgoLogic<T>
{
    decimal StopLossRatio { get; }

    void Close(AlgoEntry<T> current, decimal exitPrice, DateTime exitTime);

    void StopLoss(AlgoEntry<T> current, AlgoEntry<T> last, DateTime exitTime);
}
