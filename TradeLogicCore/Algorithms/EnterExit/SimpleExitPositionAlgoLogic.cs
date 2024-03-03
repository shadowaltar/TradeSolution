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

    public async Task<ExternalQueryState> Close(AlgoEntry? current, Security security, decimal triggerPrice, Side exitSide, DateTime exitTime, OrderActionType actionType)
    {
        if (_context.IsBackTesting) throw Exceptions.InvalidBackTestMode(false);

        // cancel any partial filled, SL or TP orders
        await _orderService.CancelAllOpenOrders(security, OrderActionType.CleanUpLive, false);

        // now close the position using algorithm only
        var asset = _portfolioService.GetAssetBySecurityId(security.Id);
        if (asset == null || asset.IsEmpty)
        {
            var message = $"Algorithm logic mismatch: we expect current algo entry is associated with an open position but it was not found / already closed.";
            _log.Warn(message);
            return ExternalQueryStates.InvalidPosition(message);
        }
        var quantity = Math.Abs(asset.Quantity);
        var residualQuantity = _context.Services.Portfolio.GetAssetPositionResidual(security.FxInfo?.BaseSecurity?.Id ?? 0);
        var residualSign = Math.Sign(residualQuantity);
        if (residualSign == -(int)exitSide)
        {
            // if residual is the opposite side of exit-side, it is time to close the residual
            quantity += Math.Abs(residualQuantity);
            _log.Info($"Discovered residual quantity for asset {security.FxInfo?.BaseSecurity?.Code}, value is {residualQuantity}; we will {exitSide} them in this close order.");
        }
        var order = new Order
        {
            Id = _orderIdGen.NewTimeBasedId,
            ExternalOrderId = _orderIdGen.NewNegativeTimeBasedId, // we may have multiple SENDING orders coexist
            AccountId = _context.AccountId,
            CreateTime = exitTime,
            UpdateTime = exitTime,
            Quantity = quantity,
            Side = exitSide,
            Status = OrderStatus.Sending,
            TimeInForce = TimeInForceType.GoodTillCancel,
            Price = 0,
            LimitPrice = 0, // MARKET order
            TriggerPrice = triggerPrice,
            Type = OrderType.Market,
            Security = security,
            SecurityId = security.Id,
            SecurityCode = security.Code,
            Action = actionType,
            Comment = Comments.AlgoExit,
        };
        return await _orderService.SendOrder(order);
    }

    public void BackTestClose(AlgoEntry current, decimal exitPrice, DateTime exitTime)
    {
        if (!_context.IsBackTesting) throw Exceptions.InvalidBackTestMode(true);

        Assertion.ShallNever(current.TheoreticEnterPrice is null or 0);

        if (exitPrice == 0 || !exitPrice.IsValid() || exitTime == DateTime.MinValue)
        {
            _log.Warn("Invalid arguments.");
            return;
        }

        var enterPrice = current.TheoreticEnterPrice!.Value;
        var r = (exitPrice - enterPrice) / enterPrice;

        current.LongCloseType = CloseType.Normal;
        current.ShortCloseType = CloseType.Normal;

        current.TheoreticExitPrice = exitPrice;
        
        // apply fee when a new position is closed
        FeeLogic?.ApplyFee(current);

        _log.Info($"action=close|p1={current.TheoreticExitPrice:F2}|p0={current.TheoreticEnterPrice:F2}|q={current.Quantity:F2}|r={r:P2}|rpnl={current.TheoreticPnl:F2}");
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

        var enterPrice = current.TheoreticEnterPrice!.Value;
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
        current.TheoreticExitPrice = exitPrice;
        
        _log.Info($"action=stopLoss|p1={exitPrice:F2}|p0={current.TheoreticEnterPrice:F2}|q={current.Quantity:F2}|r={r:P2}|rpnl={current.TheoreticPnl:F2}");
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