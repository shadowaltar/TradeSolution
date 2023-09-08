using Common;
using log4net;
using TradeLogicCore.Algorithms.FeeCalculation;
using TradeLogicCore.Algorithms.Sizing;

namespace TradeLogicCore.Algorithms.EnterExit;

public class SimpleEnterPositionAlgoLogic<T> : IEnterPositionAlgoLogic<T> where T : IAlgorithmVariables
{
    private static readonly ILog _log = Logger.New();
    public IAlgorithm<T> Algorithm { get; }
    public IPositionSizingAlgoLogic<T> Sizing { get; }
    public ITransactionFeeLogic<T>? FeeLogic { get; set; }

    public SimpleEnterPositionAlgoLogic(IAlgorithm<T> algorithm)
    {
        Sizing = algorithm.Sizing;
    }

    public void Open(AlgoEntry<T> current, AlgoEntry<T> last, decimal enterPrice, DateTime enterTime, decimal stopLossPrice)
    {
        var securityId = current.SecurityId;
        current.IsLong = true;
        current.LongCloseType = CloseType.None;
        // TODO current sizing happens here
        var asset = Algorithm.Services.Portfolio.GetAsset(securityId);
        var size = Sizing.GetSize(context.Portfolio.FreeCash, current, last, enterPrice, enterTime);
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

        // apply fee when a new position is opened
        FeeLogic?.ApplyFee(current);

        _log.Info($"action=open|time0={current.EnterTime:yyMMdd-HHmm}|p0={current.EnterPrice}|q={current.Quantity}");
    }
}
