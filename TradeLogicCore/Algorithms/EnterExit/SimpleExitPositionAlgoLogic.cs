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
        Assertion.ShallNever(current.EnterPrice == null || current.EnterPrice == 0);
        if (exitPrice == 0 || !exitPrice.IsValid() || exitTime == DateTime.MinValue)
        {
            _log.Warn("Invalid arguments.");
            return;
        }

        Context.OpenedEntries[current.Id] = current;
        var enterPrice = current.EnterPrice!.Value;
        var r = (exitPrice - enterPrice) / enterPrice;

        Assertion.ShallNever(r < StopLossRatio * -1);

        if (current.IsLong)
        {
            current.IsLong = false;
            current.LongCloseType = CloseType.Normal;
        }
        if (current.IsShort)
        {
            current.IsShort = false;
            current.ShortCloseType = CloseType.Normal;
        }
        current.ExitPrice = exitPrice;
        current.Elapsed = exitTime - current.EnterTime!.Value;
        current.RealizedPnl = (exitPrice - enterPrice) * current.Quantity;
        current.UnrealizedPnl = 0;
        current.RealizedReturn = r;
        current.Notional = current.Quantity * exitPrice;

        _log.Info($"action=close|p1={current.ExitPrice:F2}|p0={current.EnterPrice:F2}|q={current.Quantity:F2}|r={r:P2}|rpnl={current.RealizedPnl:F2}");
    }

    public void StopLoss(AlgoEntry<T> current, AlgoEntry<T> last, DateTime exitTime)
    {
        Context.OpenedEntries[current.Id] = current;

        var enterPrice = current.EnterPrice!.Value;
        var exitPrice = current.SLPrice!.Value;
        var r = (exitPrice - enterPrice) / enterPrice;

        if (current.IsLong)
        {
            current.IsLong = false;
            current.LongCloseType = CloseType.StopLoss;
        }
        if (current.IsShort)
        {
            current.IsShort = false;
            current.ShortCloseType = CloseType.StopLoss;
        }
        current.ExitPrice = exitPrice;
        current.Elapsed = exitTime - current.EnterTime!.Value;
        current.UnrealizedPnl = 0;
        current.RealizedPnl = (exitPrice - enterPrice) * current.Quantity;
        current.RealizedReturn = r;
        current.Notional = current.Quantity * exitPrice;

        _log.Info($"action=stopLoss|p1={exitPrice:F2}|p0={current.EnterPrice:F2}|q={current.Quantity:F2}|r={r:P2}|rpnl={current.RealizedPnl:F2}");
        Assertion.ShallNever(r < StopLossRatio * -1);
    }

}