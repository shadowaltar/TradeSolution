using Common;
using log4net;
using OfficeOpenXml.Style;
using TradeCommon.Essentials.Algorithms;
using TradeCommon.Essentials.Trading;
using TradeCommon.Runtime;
using TradeCommon.Utils;
using TradeLogicCore.Algorithms.FeeCalculation;
using TradeLogicCore.Services;

namespace TradeLogicCore.Algorithms.EnterExit;

public class ExternalStopOrderEnterPositionAlgoLogic : SimpleEnterPositionAlgoLogic
{
    private static readonly ILog _log = Logger.New();

    private IPortfolioService _portfolioService => _context.Services.Portfolio;
    private IOrderService _orderService => _context.Services.Order;

    private readonly IdGenerator _orderIdGen;
    private readonly Context _context;

    public ITransactionFeeLogic? FeeLogic { get; set; }

    public bool IsOpening { get; private set; }

    public ExternalStopOrderEnterPositionAlgoLogic(Context context) : base(context)
    {
        _orderIdGen = IdGenerators.Get<Order>();
        _context = context;
    }

    public override async Task OpenStopLossOrder(decimal stopLossPrice,
                                                 decimal enterPrice,
                                                 AlgoEntry current,
                                                 Side side,
                                                 DateTime enterTime,
                                                 decimal size,
                                                 long parentOrderId,
                                                 ExternalQueryState parentState)
    {
        if (stopLossPrice.IsValid() && stopLossPrice > 0)
        {
            if (stopLossPrice >= enterPrice)
            {
                _log.Error($"Cannot create a stop loss order where its price {stopLossPrice} is larger than or equals to the parent order's enter price {enterPrice}.");
            }
            else
            {
                var slSide = side == Side.Buy ? Side.Sell : Side.Buy;
                var slOrder = CreateOrder(OrderType.Stop, slSide, enterTime, 0, size, current.Security,
                    OrderActionType.AlgoStopLoss, stopLossPrice, Comments.AlgoStopLossMarket);
                slOrder.ParentOrderId = parentOrderId;
                slOrder.TriggerPrice = enterPrice;
                var subState = await _orderService.SendOrder(slOrder);
                parentState.SubStates ??= new();
                parentState.SubStates.Add(subState);
                if (subState.ResultCode == ResultCode.SendOrderFailed)
                {
                    _log.Error($"Failed to submit stop loss order! Fallback to tick-based stop loss order: [{slOrder.Id}][{slOrder.SecurityCode}] STOPPRX:{stopLossPrice}, ENTERPRX:{enterPrice}");
                }
            }
        }
    }

    public override async Task OpenTakeProfitOrder(decimal takeProfitPrice,
                                                   decimal enterPrice,
                                                   AlgoEntry current,
                                                   Side side,
                                                   DateTime enterTime,
                                                   decimal size,
                                                   long parentOrderId,
                                                   ExternalQueryState parentState)
    {
        if (takeProfitPrice.IsValid() && takeProfitPrice > 0)
        {
            if (takeProfitPrice <= enterPrice)
            {
                _log.Error($"Cannot create a take profit order where its price {takeProfitPrice} is smaller than or equals to the parent order's enter price {enterPrice}.");
            }
            else
            {
                var tpSide = side == Side.Buy ? Side.Sell : Side.Buy;
                var tpOrder = CreateOrder(OrderType.TakeProfit, tpSide, enterTime, 0, size, current.Security,
                    OrderActionType.AlgoTakeProfit, takeProfitPrice, Comments.AlgoTakeProfitMarket);
                tpOrder.ParentOrderId = parentOrderId;
                tpOrder.TriggerPrice = enterPrice;
                var subState = await _orderService.SendOrder(tpOrder);
                parentState.SubStates ??= new();
                parentState.SubStates.Add(subState);
                if (subState.ResultCode == ResultCode.SendOrderFailed)
                {
                    _log.Error($"Failed to submit take profit order! Fallback to tick-based take profit order: [{tpOrder.Id}][{tpOrder.SecurityCode}] STOPPRX:{takeProfitPrice}, ENTERPRX:{enterPrice}");
                }
            }
        }
    }
}
