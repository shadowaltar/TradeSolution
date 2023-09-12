using Common;
using log4net;
using System.Drawing;
using TradeCommon.Essentials.Instruments;
using TradeCommon.Essentials.Trading;
using TradeCommon.Runtime;
using TradeLogicCore.Algorithms.FeeCalculation;
using TradeLogicCore.Algorithms.Sizing;
using TradeLogicCore.Services;

namespace TradeLogicCore.Algorithms.EnterExit;

public class SimpleEnterPositionAlgoLogic<T> : IEnterPositionAlgoLogic<T> where T : IAlgorithmVariables
{
    private static readonly ILog _log = Logger.New();

    private readonly IPortfolioService _portfolioService;
    private readonly IOrderService _orderService;
    private readonly IdGenerator _orderIdGen;

    public IAlgorithm<T> Algorithm { get; }

    public IPositionSizingAlgoLogic<T> Sizing { get; }

    public ITransactionFeeLogic<T>? FeeLogic { get; set; }

    public SimpleEnterPositionAlgoLogic(IAlgorithm<T> algorithm)
    {
        Sizing = algorithm.Sizing;
        Algorithm = algorithm;
        _portfolioService = algorithm.AlgorithmContext.Services.Portfolio;
        _orderService = algorithm.AlgorithmContext.Services.Order;

        _orderIdGen = IdGenerators.Get<Order>();
    }

    public Order Open(AlgoEntry<T> current,
                      AlgoEntry<T> last,
                      decimal enterPrice,
                      Side side,
                      DateTime enterTime,
                      decimal stopLossPrice,
                      decimal takeProfitPrice)
    {
        if (Algorithm.AlgorithmContext.IsBackTesting) throw Exceptions.InvalidBackTestMode(false);

        var securityId = current.SecurityId;
        var asset = _portfolioService.GetPositionRelatedCurrencyAsset(securityId);
        var size = Sizing.GetSize(asset.Quantity, current, last, enterPrice, enterTime);

        var now = DateTime.UtcNow;
        var context = Algorithm.AlgorithmContext.Context;

        var order = new Order
        {
            Id = _orderIdGen.NewTimeBasedId,
            SecurityId = securityId,
            AccountId = asset.AccountId,
            BrokerId = context.BrokerId,
            Side = side,
            CreateTime = now,
            UpdateTime = now,
            ExchangeId = context.ExchangeId,
            Type = OrderType.Limit,
            Price = 0,
            Quantity = size,
            SecurityCode = current.Security.Code,
        };
        _orderService.SendOrder(order);
        return order;
    }

    public void BackTestOpen(AlgoEntry<T> current,
                             AlgoEntry<T> last,
                             decimal enterPrice,
                             Side side,
                             DateTime enterTime,
                             decimal stopLossPrice,
                             decimal takeProfitPrice)
    {
        if (!Algorithm.AlgorithmContext.IsBackTesting) return;

        var securityId = current.SecurityId;
        current.IsLong = true;
        current.LongCloseType = CloseType.None;
        // TODO current sizing happens here
        var asset = _portfolioService.GetPositionRelatedCurrencyAsset(securityId);
        var size = Sizing.GetSize(asset.Quantity, current, last, enterPrice, enterTime);

        SyncOpenOrderToEntry(current, size, enterPrice, enterTime, stopLossPrice, takeProfitPrice);

        // apply fee when a new position is opened
        FeeLogic?.ApplyFee(current);

        _log.Info($"action=open|time0={current.EnterTime:yyMMdd-HHmm}|p0={current.EnterPrice}|q={current.Quantity}");
    }

    public void SyncOpenOrderToEntry(AlgoEntry<T> current, decimal size, decimal enterPrice, DateTime enterTime, decimal stopLossPrice, decimal takeProfitPrice)
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
        current.SLPrice = stopLossPrice;
        current.TPPrice = takeProfitPrice;
        current.Notional = size * enterPrice;
    }
}
