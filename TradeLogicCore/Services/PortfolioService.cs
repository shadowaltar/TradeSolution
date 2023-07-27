using Common;
using TradeCommon.Essentials.Instruments;
using TradeCommon.Essentials.Portfolios;
using TradeCommon.Essentials.Trading;
using TradeCommon.Database;

namespace TradeLogicCore.Services;
public class PortfolioService : IPortfolioService
{
    private readonly MessageBroker<IPersistenceTask> _broker = new();
    private readonly IOrderService _orderService;
    private readonly ITradeService _tradeService;

    public PortfolioService(IOrderService orderService, ITradeService tradeService)
    {
        _orderService = orderService;
        _tradeService = tradeService;

        _orderService.NewOrder += OnNewOrder;
        _tradeService.NewTrade += OnNewTrade;
    }

    private void OnNewOrder(Order order)
    {
        throw new NotImplementedException();
    }

    private void OnNewTrade(Trade trade)
    {
        throw new NotImplementedException();
    }

    public List<Position> GetAllPositions()
    {
        throw new NotImplementedException();
    }

    public List<Balance> GetCurrentBalances()
    {
        throw new NotImplementedException();
    }

    public List<Balance> GetExternalBalances(string externalName)
    {
        throw new NotImplementedException();
    }

    public List<Position> GetPositions(string externalName, SecurityType securityType)
    {
        throw new NotImplementedException();
    }

    public List<ProfitLoss> GetRealizedPnl(Security security, DateTime rangeStart, DateTime rangeEnd)
    {
        throw new NotImplementedException();
    }

    public ProfitLoss GetUnrealizedPnl(Security security)
    {
        throw new NotImplementedException();
    }

    public Task Initialize()
    {
        throw new NotImplementedException();
    }

    public Task Persist()
    {
        throw new NotImplementedException();
    }
}
