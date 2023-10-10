using Common;
using log4net;
using TradeCommon.Essentials.Algorithms;
using TradeCommon.Essentials.Instruments;
using TradeCommon.Essentials.Trading;
using TradeCommon.Runtime;
using TradeCommon.Utils;
using TradeLogicCore.Algorithms.FeeCalculation;
using TradeLogicCore.Algorithms.Sizing;
using TradeLogicCore.Services;

namespace TradeLogicCore.Algorithms.EnterExit;

public class SimpleEnterPositionAlgoLogic : IEnterPositionAlgoLogic
{
    private static readonly ILog _log = Logger.New();

    private IPortfolioService _portfolioService => _context.Services.Portfolio;
    private IOrderService _orderService => _context.Services.Order;

    private readonly IdGenerator _orderIdGen;
    private readonly Context _context;

    private IPositionSizingAlgoLogic _sizing;

    public IPositionSizingAlgoLogic Sizing
    {
        get
        {
            _sizing ??= _context.GetAlgorithm().Sizing;
            return _sizing;
        }
    }

    public ITransactionFeeLogic? FeeLogic { get; set; }

    public SimpleEnterPositionAlgoLogic(Context context)
    {
        _orderIdGen = IdGenerators.Get<Order>();
        _context = context;
    }

    public async Task<ExternalQueryState> Open(AlgoEntry current,
                                               AlgoEntry? last,
                                               decimal enterPrice,
                                               Side side,
                                               DateTime enterTime,
                                               decimal stopLossPrice,
                                               decimal takeProfitPrice)
    {
        if (_context.IsBackTesting) throw Exceptions.InvalidBackTestMode(false);

        var asset = _portfolioService.GetAssetBySecurityId(current.Security.QuoteSecurity.Id)
            ?? throw Exceptions.MissingAssetPosition(current.Security.QuoteSecurity.Code);

        var size = Sizing.GetSize(asset.Quantity, current, last, enterPrice, enterTime);
        var order = CreateOrder(OrderType.Limit, side, enterTime, enterPrice, size, current.Security, comment: Comments.AlgoEnter);

        current.OrderId = order.Id;

        var state = await _orderService.SendOrder(order);
        state.SubStates = new();

        if (stopLossPrice.IsValid())
        {
            if (stopLossPrice >= enterPrice)
            {
                _log.Error($"Cannot create a stop loss limit order where its price {stopLossPrice} is larger than or equals to the parent order's limit price {enterPrice}.");
            }
            else
            {
                var slSide = side == Side.Buy ? Side.Sell : Side.Buy;
                var stopPrice = (stopLossPrice + enterPrice) / 2m; // trigger price at the mid
                var slOrder = CreateOrder(OrderType.StopLimit, slSide, enterTime, stopLossPrice, size, current.Security, stopPrice, Comments.AlgoStopLoss);
                var subState = await _orderService.SendOrder(slOrder);
                state.SubStates.Add(subState);
            }
        }

        if (takeProfitPrice.IsValid())
        {
            if (takeProfitPrice <= enterPrice)
            {
                _log.Error($"Cannot create a take profit limit order where its price {takeProfitPrice} is smaller than or equals to the parent order's limit price {enterPrice}.");
            }
            else
            {
                var tpSide = side == Side.Buy ? Side.Sell : Side.Buy;
                var stopPrice = (takeProfitPrice + enterPrice) / 2m; // trigger price at the mid
                var tpOrder = CreateOrder(OrderType.TakeProfitLimit, tpSide, enterTime, takeProfitPrice, size, current.Security, stopPrice, Comments.AlgoTakeProfit);
                var subState = await _orderService.SendOrder(tpOrder);
                state.SubStates.Add(subState);
            }
        }
        return state;
    }

    private Order CreateOrder(OrderType type, Side side, DateTime time, decimal price, decimal quantity, Security security, decimal? stopPrice = null, string comment = "")
    {
        if (side == Side.None) throw Exceptions.Invalid<Side>(side);
        if (!time.IsValid()) throw Exceptions.Invalid<DateTime>(time);
        if (!price.IsValid() || price <= 0) throw Exceptions.Invalid<decimal>(price);
        if (!quantity.IsValid() || quantity <= 0) throw Exceptions.Invalid<decimal>(quantity);
        if (!security.IsValid()) throw Exceptions.Invalid<Security>(security);
        if (!security.QuoteSecurity.IsValid()) throw Exceptions.Invalid<Security>("Security's quote security is: " + security.QuoteSecurity);
        if (stopPrice != null && (!stopPrice.IsValid() || stopPrice <= 0)) throw Exceptions.Invalid<decimal>(stopPrice);

        var assetPosition = _context.Services.Portfolio.GetAssetBySecurityId(security.QuoteSecurity.Id);
        if (assetPosition == null || !assetPosition.Security.IsValid()) throw Exceptions.Invalid<Security>("asset position security is: " + assetPosition?.Security);
        if (assetPosition.Quantity < quantity)
            throw Exceptions.InvalidOrder($"Insufficient quote asset to be traded. Existing: {assetPosition.Quantity}; desired: {quantity}");

        var order = new Order
        {
            Id = _orderIdGen.NewTimeBasedId,
            AccountId = _context.AccountId,
            Side = side,
            CreateTime = time,
            UpdateTime = time,
            Type = type,
            Price = price,
            Quantity = quantity,
            Security = security,
            SecurityId = security.Id,
            SecurityCode = security.Code,
            Status = OrderStatus.Sending,
            TimeInForce = TimeInForceType.GoodTillCancel,
            Comment = comment,
        };
        if (stopPrice != null)
        {
            order.StopPrice = stopPrice.Value;
        }
        return order;
    }

    public void BackTestOpen(AlgoEntry current,
                             AlgoEntry? last,
                             decimal enterPrice,
                             Side side,
                             DateTime enterTime,
                             decimal stopLossPrice,
                             decimal takeProfitPrice)
    {
        if (!_context.IsBackTesting) return;

        // TODO current sizing happens here
        var asset = _portfolioService.GetAssetBySecurityId(current.Security.QuoteSecurity.Id);
        var size = Sizing.GetSize(asset.Quantity, current, last, enterPrice, enterTime);

        SyncOpenOrderToEntry(current, size, enterPrice, enterTime, stopLossPrice, takeProfitPrice);

        // apply fee when a new position is opened
        FeeLogic?.ApplyFee(current);

        _log.Info($"action=open|time0={current.EnterTime:yyMMdd-HHmm}|p0={current.EnterPrice}|q={current.Quantity}");
    }

    public void SyncOpenOrderToEntry(AlgoEntry current, decimal size, decimal enterPrice, DateTime enterTime, decimal stopLossPrice, decimal takeProfitPrice)
    {
        // this method should take care of multiple fills
        current.Quantity = size;
        current.EnterPrice = enterPrice;
        current.EnterTime = enterTime;
        current.ExitPrice = 0;
        if (current.Elapsed == null)
            current.Elapsed = TimeSpan.Zero;
        else if (current.EnterTime != null)
            current.Elapsed = enterTime - current.EnterTime; // happens at 2nd or later fills
        current.RealizedPnl = 0;
        current.UnrealizedPnl = 0;
        current.RealizedReturn = 0;
        current.StopLossPrice = stopLossPrice;
        current.TakeProfitPrice = takeProfitPrice;
        current.Notional = size * enterPrice;
    }
}
