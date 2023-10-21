using Common;
using log4net;
using TradeCommon.Essentials.Algorithms;
using TradeCommon.Essentials.Instruments;
using TradeCommon.Essentials.Trading;
using TradeCommon.Runtime;
using TradeCommon.Utils;
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

    public readonly object _closeFlagLock = new();

    public bool IsClosing { get; private set; }

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

    public async Task<ExternalQueryState> Close(AlgoEntry? current, Security security, Side exitSide, DateTime exitTime)
    {
        if (_context.IsBackTesting) throw Exceptions.InvalidBackTestMode(false);

        try
        {
            lock (_closeFlagLock)
            {
                if (IsClosing)
                    return ExternalQueryStates.CloseConflict(security.Code);
                IsClosing = true;
            }

            // cancel any partial filled, SL or TP orders
            await _orderService.CancelAllOpenOrders(security);

            // now close the position using algorithm only
            var position = _portfolioService.GetPositionBySecurityId(security.Id);
            if (position == null || position.IsClosed)
            {
                var message = $"Algorithm logic mismatch: we expect current algo entry is associated with an open position but it was not found / already closed.";
                _log.Warn(message);
                return ExternalQueryStates.InvalidPosition(message);
            }
            var order = new Order
            {
                Id = _orderIdGen.NewTimeBasedId,
                ExternalOrderId = _orderIdGen.NewNegativeTimeBasedId, // we may have multiple SENDING orders coexist
                AccountId = _context.AccountId,
                CreateTime = exitTime,
                UpdateTime = exitTime,
                Quantity = Math.Abs(position.Quantity),
                Side = exitSide,
                Status = OrderStatus.Sending,
                TimeInForce = TimeInForceType.GoodTillCancel,
                Price = 0,
                Type = OrderType.Market,
                Security = security,
                SecurityId = security.Id,
                SecurityCode = security.Code,
                Comment = Comments.AlgoExit,
            };

            _log.Info(PrintOrder(order));
            var state = await _orderService.SendOrder(order);
            if (state.ResultCode == ResultCode.SendOrderOk)
            {
            }
            else if (state.ResultCode == ResultCode.SendOrderFailed)
            {

            }
            return state;
        }
        finally
        {
            lock (_closeFlagLock)
                IsClosing = false;
        }
    }

    private string PrintOrder(Order order)
    {
        var filledQtyStr = "";
        if (order.Status == OrderStatus.PartialFilled)
            filledQtyStr = ", FILLQTY:" + order.FormattedFilledQuantity;
        if (order.Type == OrderType.StopLimit || order.Type == OrderType.TakeProfitLimit)
            return $"\n\tORD: [AlgoExit][{order.UpdateTime:HHmmss}][{order.SecurityCode}][{order.Type}][{order.Side}][{order.Status}]\n\t\tID:{order.Id}, SLPRX:{order.FormattedStopPrice}, QTY:{order.FormattedQuantity}{filledQtyStr}";
        else
            return $"\n\tORD: [AlgoExit][{order.UpdateTime:HHmmss}][{order.SecurityCode}][{order.Type}][{order.Side}][{order.Status}]\n\t\tID:{order.Id}, PRX:{order.FormattedPrice}, QTY:{order.FormattedQuantity}{filledQtyStr}";
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

        current.LongCloseType = CloseType.Normal;
        current.ShortCloseType = CloseType.Normal;

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
        var side = _portfolioService.GetOpenPositionSide(current.SecurityId);
        if (side == Side.None) return;

        decimal slRatio = side switch
        {
            Side.Buy => LongStopLossRatio,
            Side.Sell => -ShortStopLossRatio,
            _ => throw Exceptions.InvalidSide(),
        };
        if (!slRatio.IsValid() && slRatio <= 0)
            return;

        var enterPrice = current.EnterPrice!.Value;
        var exitPrice = current.Security.GetStopLossPrice(enterPrice, slRatio);
        var r = (exitPrice - enterPrice) / enterPrice;

        if (side == Side.Buy)
        {
            current.LongCloseType = CloseType.StopLoss;
        }
        if (side == Side.Sell)
        {
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