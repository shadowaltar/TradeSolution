using Common;
using log4net;
using TradeCommon.Essentials.Trading;
using TradeLogicCore.Algorithms.FeeCalculation;
using TradeLogicCore.Services;

namespace TradeLogicCore.Algorithms.EnterExit;

public class SimpleExitPositionAlgoLogic<T> : IExitPositionAlgoLogic<T> where T : IAlgorithmVariables
{
    private static readonly ILog _log = Logger.New();

    private readonly IAlgorithmContext<T> _algoContext;
    private readonly IOrderService _orderService;
    private readonly IPortfolioService _portfolioService;

    public ITransactionFeeLogic<T>? FeeLogic { get; set; }

    public decimal StopLossRatio { get; }

    public decimal TakeProfitRatio { get; }

    public IAlgorithm<T> MainAlgo { get; }

    public SimpleExitPositionAlgoLogic(IAlgorithm<T> mainAlgo, decimal stopLossRatio, decimal takeProfitRatio)
    {
        MainAlgo = mainAlgo;
        _algoContext = mainAlgo.Context;
        _orderService = _algoContext.Services.Order;
        _portfolioService = _algoContext.Services.Portfolio;

        StopLossRatio = stopLossRatio;
        TakeProfitRatio = takeProfitRatio;

        _orderService.OrderClosed -= OnCloseOrderAcknowledged;
        _orderService.OrderClosed += OnCloseOrderAcknowledged;
        _orderService.OrderStoppedLost -= OnStopLossTriggered;
        _orderService.OrderStoppedLost += OnStopLossTriggered;
        _orderService.OrderTookProfit -= OnTakeProfitTriggered;
        _orderService.OrderTookProfit += OnTakeProfitTriggered;
    }

    public void Close(AlgoEntry<T> current, decimal exitPrice, DateTime exitTime)
    {
        if (_algoContext.IsBackTesting)
        {
            BackTestClose(current, exitPrice, exitTime);
              }
        else
        {
            var position = _portfolioService.GetPosition(current.PositionId);
            if (position == null)
            {
                _log.Error($"Algorithm logic mismatch: we expect current algo entry is associated with an open position {current.PositionId} but it was not found / already closed.");
                return;
            }
            _orderService.CreateCloseOrderAndSend(position, OrderType.Market, decimal.MinValue, TimeInForceType.GoodTillCancel); ;
        }
    }

    private void BackTestClose(AlgoEntry<T> current, decimal exitPrice, DateTime exitTime)
    {
        Assertion.ShallNever(current.EnterPrice == null || current.EnterPrice == 0);
        if (exitPrice == 0 || !exitPrice.IsValid() || exitTime == DateTime.MinValue)
        {
            _log.Warn("Invalid arguments.");
            return;
        }

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

        // apply fee when a new position is closed
        FeeLogic?.ApplyFee(current);

        _log.Info($"action=close|p1={current.ExitPrice:F2}|p0={current.EnterPrice:F2}|q={current.Quantity:F2}|r={r:P2}|rpnl={current.RealizedPnl:F2}");

    }

    public void StopLoss(AlgoEntry<T> current, AlgoEntry<T> last, DateTime exitTime)
    {
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

    public void OnCloseOrderAcknowledged()
    {
        throw new NotImplementedException();
    }

    public void OnStopLossTriggered()
    {
        throw new NotImplementedException();
    }

    public void OnTakeProfitTriggered()
    {
        throw new NotImplementedException();
    }
}