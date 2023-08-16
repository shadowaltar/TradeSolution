using Common;
using log4net;
using TradeLogicCore.Algorithms.Sizing;

namespace TradeLogicCore.Algorithms.EnterExit;

public class SimpleEnterPositionAlgoLogic<T> : IEnterPositionAlgoLogic<T> where T : IAlgorithmVariables
{
    private static readonly ILog _log = Logger.New();

    public IAlgorithemContext<T> Context { get; }
    public IPositionSizingAlgoLogic<T> Sizing { get; }

    public SimpleEnterPositionAlgoLogic(IAlgorithemContext<T> context, IPositionSizingAlgoLogic<T> sizing)
    {
        Context = context;
        Sizing = sizing;
    }

    public void Open(AlgoEntry<T> current, AlgoEntry<T> last, decimal enterPrice, DateTime enterTime, decimal stopLossPrice)
    {
        Assertion.ShallNever(Context.Portfolio.FreeCash == 0);

        current.IsLong = true;
        current.LongCloseType = CloseType.None;
        // TODO current sizing happens here
        var size = Sizing.GetSize(Context.Portfolio.FreeCash, current, last, enterPrice, enterTime);
        current.Quantity = size;
        current.EnterPrice = enterPrice;
        current.EnterTime = enterTime;
        current.ExitPrice = 0;
        current.Elapsed = TimeSpan.Zero;
        current.RealizedPnl = 0;
        current.UnrealizedPnl = 0;
        current.RealizedReturn = 0;
        current.SLPrice = stopLossPrice;
        current.Notional = current.Quantity * enterPrice;

        _log.Info($"action=open|time0={current.EnterTime:yyMMdd-HHmm}|p0={current.EnterPrice}|q={current.Quantity}");
    }
}
