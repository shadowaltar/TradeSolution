using Common;
using log4net;

namespace TradeLogicCore.Algorithms.EnterExit;

public class SimpleExitPositionAlgoLogic<T> : IExitPositionAlgoLogic<T> where T : IAlgorithmVariables
{
    private static readonly ILog _log = Logger.New();

    public IAlgorithemContext<T> Context { get; }

    public decimal StopLossRatio { get; }

    public SimpleExitPositionAlgoLogic(IAlgorithemContext<T> context, decimal stopLossRatio)
    {
        Context = context;
        StopLossRatio = stopLossRatio;
    }

    public void Close(AlgoEntry<T> current, decimal exitPrice, DateTime exitTime)
    {
        if (exitPrice == 0 || !exitPrice.IsValid() || exitTime == DateTime.MinValue)
        {
            _log.Warn("Invalid arguments.");
            return;
        }

        Context.OpenedEntries[current.Id] = current;

        var r = (exitPrice - current.EnterPrice) / current.EnterPrice;
        current.IsOpened = false;
        current.IsClosing = true;
        current.ExitPrice = exitPrice;
        current.ExitTime = exitTime;
        current.IsStopLossTriggered = false;
        current.RealizedPnl = (exitPrice - current.EnterPrice) * current.Quantity;
        current.UnrealizedPnl = 0;
        current.RealizedReturn = r;
        current.Notional = current.Quantity * exitPrice;

        _log.Info($"action=close|time1={current.ExitTime:yyMMdd-HHmm}|p1={current.ExitPrice:F2}|p0={current.EnterPrice:F2}|q={current.Quantity:F2}|r={r:P2}|rpnl={current.RealizedPnl:F2}");
        Assertion.ShallNever(r < StopLossRatio * -1);
    }

    public void StopLoss(AlgoEntry<T> current, AlgoEntry<T> last, DateTime exitTime)
    {
        Context.OpenedEntries[current.Id] = current;

        var r = (current.StopLossPrice - current.EnterPrice) / current.EnterPrice;
        current.IsOpened = false;
        current.IsClosing = true;
        current.ExitPrice = current.StopLossPrice;
        current.ExitTime = exitTime;
        current.IsStopLossTriggered = true;
        current.UnrealizedPnl = 0;
        current.RealizedPnl = (current.StopLossPrice - current.EnterPrice) * current.Quantity;
        current.RealizedReturn = r;
        current.Notional = current.Quantity * current.StopLossPrice;

        _log.Info($"action=stopLoss|time1={current.ExitTime:yyMMdd-HHmm}|p1={current.StopLossPrice:F2}|p0={current.EnterPrice:F2}|q={current.Quantity:F2}|r={r:P2}|rpnl={current.RealizedPnl:F2}");
        Assertion.ShallNever(r < StopLossRatio * -1);
    }

}