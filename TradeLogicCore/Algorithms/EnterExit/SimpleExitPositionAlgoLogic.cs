using Common;
using log4net;
using TradeCommon.Essentials.Algorithms;
using TradeCommon.Essentials.Trading;
using TradeCommon.Runtime;
using TradeLogicCore.Algorithms.FeeCalculation;
using TradeLogicCore.Services;

namespace TradeLogicCore.Algorithms.EnterExit;

public class SimpleExitPositionAlgoLogic : IExitPositionAlgoLogic
{
    private static readonly ILog _log = Logger.New();

    private readonly Context _context;
    private readonly IOrderService _orderService;
    private readonly IPortfolioService _portfolioService;
    private readonly IdGenerator _orderIdGen;

    public ITransactionFeeLogic? FeeLogic { get; set; }

    public decimal LongStopLossRatio { get; }

    public decimal LongTakeProfitRatio { get; }

    public decimal ShortStopLossRatio { get; }

    public decimal ShortTakeProfitRatio { get; }

    public SimpleExitPositionAlgoLogic(Context context,
                                       decimal longSL = decimal.MinValue,
                                       decimal longTP = decimal.MinValue,
                                       decimal shortSL = decimal.MinValue,
                                       decimal shortTP = decimal.MinValue)
    {
        _context = context;
        _orderService = context.Services.Order;
        _portfolioService = context.Services.Portfolio;

        LongStopLossRatio = longSL;
        LongTakeProfitRatio = longTP;
        ShortStopLossRatio = shortSL;
        ShortTakeProfitRatio = shortTP;

        _orderService.OrderClosed -= OnCloseOrderAcknowledged;
        _orderService.OrderClosed += OnCloseOrderAcknowledged;
        _orderService.OrderStoppedLost -= OnStopLossTriggered;
        _orderService.OrderStoppedLost += OnStopLossTriggered;
        _orderService.OrderTookProfit -= OnTakeProfitTriggered;
        _orderService.OrderTookProfit += OnTakeProfitTriggered;

        _orderIdGen = IdGenerators.Get<Order>();
    }

    public void Close(AlgoEntry current, decimal exitPrice, DateTime exitTime)
    {
        if (_context.IsBackTesting) throw Exceptions.InvalidBackTestMode(false);

        var position = _portfolioService.GetPosition(current.SecurityId);
        if (position == null)
        {
            _log.Error($"Algorithm logic mismatch: we expect current algo entry is associated with an open position {current.PositionId} but it was not found / already closed.");
            return;
        }
        var order = new Order
        {
            Id = _orderIdGen.NewTimeBasedId,
            AccountId = position.AccountId,
            SecurityCode = current.Security.Code,
            SecurityId = position.SecurityId,
            BrokerId = _context.BrokerId,
            CreateTime = exitTime,
            ExchangeId = _context.ExchangeId,
            Quantity = position.Quantity,
            Side = Side.Sell,
            Status = OrderStatus.Submitting,
            TimeInForce = TimeInForceType.GoodTillCancel,
            Price = exitPrice,
            Type = OrderType.Limit,
        };
        _orderService.SendOrder(order);
    }

    public void BackTestClose(AlgoEntry current, decimal exitPrice, DateTime exitTime)
    {
        if (!_context.IsBackTesting) throw Exceptions.InvalidBackTestMode(true);

        Assertion.ShallNever(current.EnterPrice is null or 0);

        if (exitPrice == 0 || !exitPrice.IsValid() || exitTime == DateTime.MinValue)
        {
            _log.Warn("Invalid arguments.");
            return;
        }

        var enterPrice = current.EnterPrice!.Value;
        var r = (exitPrice - enterPrice) / enterPrice;

        if (current.IsLong)
        {
            Assertion.ShallNever(r < LongStopLossRatio * -1);
            current.IsLong = false;
            current.LongCloseType = CloseType.Normal;
        }
        if (current.IsShort)
        {
            Assertion.ShallNever(r < ShortStopLossRatio * -1);
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

    public void BackTestStopLoss(AlgoEntry current, AlgoEntry last, DateTime exitTime)
    {
        var enterPrice = current.EnterPrice!.Value;
        var exitPrice = current.StopLossPrice!.Value;
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
        Assertion.ShallNever(r < LongStopLossRatio * -1);
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