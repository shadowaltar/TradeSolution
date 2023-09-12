using Common;
using log4net;
using System.Drawing;
using System.Security.Cryptography;
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

    private IPortfolioService _portfolioService => _context.Services.Portfolio;
    private IOrderService _orderService => _context.Services.Order;

    private readonly IdGenerator _orderIdGen;
    private readonly Context _context;

    private IPositionSizingAlgoLogic<T> _sizing;
    private IAlgorithmEngine<T> _algorithmEngine;

    public IPositionSizingAlgoLogic<T> Sizing
    {
        get
        {
            _sizing ??= _context.GetAlgorithm<T>().Sizing;
            return _sizing;
        }
    }

    public ITransactionFeeLogic<T>? FeeLogic { get; set; }

    public SimpleEnterPositionAlgoLogic(Context context)
    {
        _orderIdGen = IdGenerators.Get<Order>();
        _context = context;
    }

    public Order Open(AlgoEntry<T> current,
                      AlgoEntry<T> last,
                      decimal enterPrice,
                      Side side,
                      DateTime enterTime,
                      decimal stopLossPrice,
                      decimal takeProfitPrice)
    {
        if (_context.IsBackTesting) throw Exceptions.InvalidBackTestMode(false);

        var securityId = current.SecurityId;
        var asset = _portfolioService.GetPositionRelatedCurrencyAsset(securityId);
        var size = Sizing.GetSize(asset.Quantity, current, last, enterPrice, enterTime);

        var now = DateTime.UtcNow;
        var order = new Order
        {
            Id = _orderIdGen.NewTimeBasedId,
            SecurityId = securityId,
            AccountId = asset.AccountId,
            BrokerId = _context.BrokerId,
            Side = side,
            CreateTime = now,
            UpdateTime = now,
            ExchangeId = _context.ExchangeId,
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
        if (!_context.IsBackTesting) return;

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
