﻿using Common;
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

    public bool IsOpening { get; private set; }

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

        try
        {
            IsOpening = true;

            if (_context.IsBackTesting) throw Exceptions.InvalidBackTestMode(false);

            var asset = _portfolioService.GetAssetBySecurityId(current.Security.QuoteSecurity.Id)
                ?? throw Exceptions.MissingAssetPosition(current.Security.QuoteSecurity.Code);

            var size = Sizing.GetSize(asset.Quantity, current, last, enterPrice, enterTime);
            if (size <= 0)
            {
                return ExternalQueryStates.InvalidOrder($"Current asset quantity = {asset.Quantity}", "", "Quantity must be positive");
            }
            var order = CreateOrder(OrderType.Market, side, enterTime, 0, size, current.Security,
                OrderActionType.AlgoOpen, comment: Comments.AlgoEnterMarket);
            order.TriggerPrice = enterPrice;
            var state = await _orderService.SendOrder(order);
            if (state.ResultCode == ResultCode.SendOrderOk)
            {
                await OpenStopLossOrder(stopLossPrice, enterPrice, current, side, enterTime, size, order.Id, state);
                await OpenTakeProfitOrder(takeProfitPrice, enterPrice, current, side, enterTime, size, order.Id, state);
            }
            return state;
        }
        finally
        {
            IsOpening = false;
        }
    }

    public virtual async Task OpenStopLossOrder(decimal stopLossPrice,
                                                decimal enterPrice,
                                                AlgoEntry current,
                                                Side side,
                                                DateTime enterTime,
                                                decimal size,
                                                long parentOrderId,
                                                ExternalQueryState parentState)
    {
        return;
    }

    public virtual async Task OpenTakeProfitOrder(decimal takeProfitPrice,
                                                  decimal enterPrice,
                                                  AlgoEntry current,
                                                  Side side,
                                                  DateTime enterTime,
                                                  decimal size,
                                                  long parentOrderId,
                                                  ExternalQueryState parentState)
    {
        return;
    }

    protected virtual Order CreateOrder(OrderType type,
                                        Side side,
                                        DateTime time,
                                        decimal price,
                                        decimal quantity,
                                        Security security,
                                        OrderActionType actionType,
                                        decimal? stopPrice = null,
                                        string comment = "")
    {
        if (side == Side.None) throw Exceptions.Invalid<Side>(side);
        if (!time.IsValid()) throw Exceptions.Invalid<DateTime>(time);
        if (type == OrderType.Limit && (!price.IsValid() || price == 0)) throw Exceptions.Invalid<decimal>("Limit order must specify a valid price.");
        if (type == OrderType.StopLimit && (!price.IsValid() || price == 0)) throw Exceptions.Invalid<decimal>("Stop loss limit order must specify a valid price.");
        if (type == OrderType.TakeProfitLimit && (!price.IsValid() || price == 0)) throw Exceptions.Invalid<decimal>("Take profit limit order must specify a valid price.");
        if (!quantity.IsValid() || quantity <= 0) throw Exceptions.Invalid<decimal>(quantity);
        if (!security.IsValid()) throw Exceptions.Invalid<Security>(security);
        if (!security.QuoteSecurity.IsValid()) throw Exceptions.Invalid<Security>("Security's quote security is: " + security.QuoteSecurity);
        if (stopPrice != null && (!stopPrice.IsValid() || stopPrice <= 0)) throw Exceptions.Invalid<decimal>(stopPrice);

        var assetPosition = _context.Services.Portfolio.GetAssetBySecurityId(security.QuoteSecurity.Id);
        if (assetPosition == null || !assetPosition.Security.IsValid()) throw Exceptions.Invalid<Security>("asset position security is: " + assetPosition?.Security);
        if (assetPosition.Quantity < quantity)
            throw Exceptions.InvalidOrder($"Insufficient quote asset to be traded. Existing: {assetPosition.Quantity}; desired: {quantity}");
        if (type is OrderType.Market or OrderType.Stop or OrderType.TakeProfit)
            price = 0;

        var order = new Order
        {
            Id = _orderIdGen.NewTimeBasedId,
            AccountId = _context.AccountId,
            ExternalOrderId = _orderIdGen.NewNegativeTimeBasedId, // we may have multiple SENDING orders coexist
            Side = side,
            CreateTime = time,
            UpdateTime = time,
            Type = type,
            Price = 0,
            LimitPrice = type.IsLimit() ? price : 0,
            Quantity = quantity,
            Security = security,
            SecurityId = security.Id,
            SecurityCode = security.Code,
            Status = OrderStatus.Sending,
            TimeInForce = TimeInForceType.GoodTillCancel,
            Action = actionType,
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

        SyncOpenOrderToEntry(current, size, enterPrice, enterTime);

        // apply fee when a new position is opened
        FeeLogic?.ApplyFee(current);

        _log.Info($"action=open|time0={current.EnterTime:yyMMdd-HHmm}|p0={current.EnterPrice}|q={current.Quantity}");
    }

    public void SyncOpenOrderToEntry(AlgoEntry current, decimal size, decimal enterPrice, DateTime enterTime)
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
        current.RealizedReturn = 0;
        current.Notional = size * enterPrice;
    }
}
