using Autofac;
using Common;
using log4net;
using TradeCommon.Constants;
using TradeCommon.Essentials;
using TradeCommon.Essentials.Accounts;
using TradeCommon.Essentials.Instruments;
using TradeCommon.Essentials.Trading;
using TradeCommon.Runtime;
using TradeDataCore.Instruments;
using TradeLogicCore.Algorithms;
using TradeLogicCore.Algorithms.Parameters;
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

    public ExchangeType ExchangeType { get; private set; }
    public BrokerType BrokerType { get; private set; }

    public Core(IComponentContext componentContext, IServices services)
    {
        _componentContext = componentContext;
        _services = services;
    }

    /// <summary>
    /// To start a trading algorithm, the following parameters need to be provided:
    /// * environment, broker, exchange, user and account details.
    /// * securities to be listened and screened.
    /// * algorithm instance, with position-sizing, entering, exiting, screening, fee-charging logic components.
    /// * when to start: immediately or wait for next start of min/hour/day/week etc.
    /// * when to stop: algorithm halting condition, eg. 2 hours before exchange maintenance.
    /// * what time interval is the algorithm interested in.
    /// Additionally if it is in back-testing mode:
    /// * whether it is a perpetual or ranged testing.
    /// 
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="parameters"></param>
    /// <param name="algorithm"></param>
    /// <returns></returns>
    /// <exception cref="InvalidOperationException"></exception>
    public async Task StartAlgorithm<T>(AlgoStartupParameters parameters,
                                        IAlgorithm<T> algorithm) where T : IAlgorithmVariables
    {
        _services = _componentContext.Resolve<IServices>();

        var user = await _services.Admin.ReadUser(parameters.UserName, parameters.Environment);
        if (user == null)
            throw new InvalidOperationException("The user does not exist.");

        if (!_services.Admin.Login(user, parameters.Password, parameters.Environment))
            throw new InvalidOperationException("The password is incorrect.");

        await CheckAccountAndBalance(user);
        await CheckOpenOrders();
        await CheckRecentOrderHistory();
        await CheckRecentTradeHistory();

        var engine = new AlgorithmEngine<T>(_services, algorithm);
        engine.Run(parameters.BasicSecurityPool, parameters.Interval, parameters.EffectiveTimeRange);
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
        foreach (var account in user.Accounts)
        {
            var externalAccount = await PortfolioService.GetAccountByName(account.Name, true);
            var internalAccount = await PortfolioService.GetAccountByName(account.Name);
            if (externalAccount != internalAccount)
            {
                _log.Warn("Internally stored account does not exactly match the external account.");

            }
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