using Autofac;
using Common;
using log4net;
using TradeCommon.Constants;
using TradeCommon.Essentials.Accounts;
using TradeCommon.Essentials.Trading;
using TradeCommon.Runtime;
using TradeDataCore.Instruments;
using TradeLogicCore.Services;

namespace TradeLogicCore;
public class Core
{
    private static readonly ILog _log = Logger.New();

    private readonly IComponentContext _componentContext;
    private IServices _services;

    private IPortfolioService PortfolioService => _services.Portfolio;
    private IOrderService OrderService => _services.Order;
    private ITradeService TradeService => _services.Trade;
    private ISecurityService SecurityService => _services.Security;

    public ExchangeType ExchangeType => ExchangeType.Binance;

    public Core(IComponentContext componentContext)
    {
        _componentContext = componentContext;
    }

    public async Task Start(string userName, string adminPassword, EnvironmentType environment)
    {
        // TODO admin password is not used here

        _services = _componentContext.Resolve<IServices>();

        var user = await _services.Admin.ReadUser(userName, environment);
        if (user == null)
            throw new InvalidOperationException("The user does not exist.");

        await CheckAccountAndBalance(user);
        await CheckOpenOrders();
        await CheckRecentOrderHistory();
        await CheckRecentTradeHistory();

        //SubscribeToMarketData();
        //StartAlgorithmEngine();
    }

    private Task CheckRecentOrderHistory()
    {
        throw new NotImplementedException();
    }

    private Task CheckRecentTradeHistory()
    {
        throw new NotImplementedException();
    }

    private async Task CheckAccountAndBalance(User user)
    {
        if (!PortfolioService.SelectUser(user))
        {
            Environment.Exit(StatusCodes.GetUserFailed);
            return;
        }
        foreach (var account in user.Accounts)
        {
            var externalAccount = await PortfolioService.GetAccountByName(account.Name, true);
            var internalAccount = await PortfolioService.GetAccountByName(account.Name);
        }
    }

    private async Task CheckOpenOrders()
    {
        var externalOpenOrders = await _services.Order.GetOpenOrders(null, true);
        var internalOpenOrders = await _services.Order.GetOpenOrders(null);

        var notStoredOpenOrders = new List<Order>();

        // a stored one does not exist on external side
        foreach (var order in internalOpenOrders)
        {
            if (!externalOpenOrders.Exists(o => o.ExternalOrderId == order.ExternalOrderId))
            {

            }
        }
        // an external one does not exist in storage
        foreach (var order in externalOpenOrders)
        {
            if (!internalOpenOrders.Exists(o => o.ExternalOrderId == order.ExternalOrderId))
            {

            }
        }
    }

    private bool TryRetrieveOpenedOrders(out List<Order> orders)
    {
        throw new NotImplementedException();
    }

    private void StartAlgorithmEngine()
    {
        throw new NotImplementedException();
    }

    private void SubscribeToMarketData()
    {
        throw new NotImplementedException();
    }

    private void CheckTradeHistory()
    {
        throw new NotImplementedException();
    }
}
