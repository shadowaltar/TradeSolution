using Autofac;
using Common;
using log4net;
using TradeCommon.Constants;
using TradeCommon.Database;
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

        var startTime = parameters.EffectiveTimeRange.ActualStartTime;

        await CheckAccountAndBalance(user);
        await CheckOpenOrders();

        await Task.Run(async () =>
        {
            // check one day's historical order / trade only
            // they should not impact trading
            var previousDay = startTime.AddDays(-1);
            await CheckRecentOrderHistory(previousDay, startTime, parameters.BasicSecurityPool);
            await CheckRecentTradeHistory(previousDay, startTime, parameters.BasicSecurityPool);
        });

        var engine = new AlgorithmEngine<T>(_services, algorithm);
        engine.Run(parameters.BasicSecurityPool, parameters.Interval, parameters.EffectiveTimeRange);
    }

    private async Task CheckRecentOrderHistory(DateTime start, DateTime end, List<Security> securities)
    {
        var externalOrders = new Dictionary<long, Order>();
        var internalOrders = new Dictionary<long, Order>();
        foreach (var security in securities)
        {
            var externalResults = await _services.Order.GetOrderHistory(start, end, security, true);
            if (externalResults != null)
            {
                foreach (var r in externalResults)
                {
                    externalOrders[r.ExternalOrderId] = r;
                }
            }

            var internalResults = await _services.Order.GetOrderHistory(start, end, security);
            if (internalResults != null)
            {
                foreach (var r in internalResults)
                {
                    internalOrders[r.ExternalOrderId] = r;
                }
            }
        }
        var (toCreate, toUpdate, toDelete) = FindDifferences(externalOrders, internalOrders);

        if (toCreate != null)
            _services.Persistence.Enqueue(new PersistenceTask<Order>(toCreate) { ActionType = DatabaseActionType.Create });
        if (toUpdate != null)
            _services.Persistence.Enqueue(new PersistenceTask<Order>(toUpdate.Values.ToList()) { ActionType = DatabaseActionType.Update });
    }

    public static (List<T>, Dictionary<long, T>, List<long>) FindDifferences<T>(Dictionary<long, T> primary, Dictionary<long, T> secondary)
        where T : IComparable<T>
    {
        var toCreate = new List<T>();
        var toUpdate = new Dictionary<long, T>();
        var toDelete = new List<long>();
        foreach (var (id, external) in primary)
        {
            if (secondary.TryGetValue(id, out var @internal))
            {
                if (@internal.CompareTo(external) != 0)
                    toUpdate[id] = external;
            }
            else
            {
                toCreate.Add(external);
            }
        }
        foreach (var (id, @internal) in secondary)
        {
            if (primary.TryGetValue(id, out var external))
            {
                if (@internal.CompareTo(external) != 0)
                    toUpdate[id] = external;
            }
            else
            {
                toDelete.Add(id);
            }
        }
        return (toCreate, toUpdate, toDelete);
    }

    private async Task CheckRecentTradeHistory(DateTime start, DateTime end, List<Security> securities)
    {
        var externalTrades = new Dictionary<long, Trade>();
        var internalTrades = new Dictionary<long, Trade>();
        foreach (var security in securities)
        {
            var externalResults = await _services.Trade.GetTradeHistory(start, end, security, true);
            if (externalResults != null)
            {
                foreach (var r in externalResults)
                {
                    externalTrades[r.ExternalTradeId] = r;
                }
            }

            var internalResults = await _services.Trade.GetTradeHistory(start, end, security);
            if (internalResults != null)
            {
                foreach (var r in internalResults)
                {
                    internalTrades[r.ExternalTradeId] = r;
                }
            }
        }
        var (toCreate, toUpdate, toDelete) = FindDifferences(externalTrades, internalTrades);
        if (toCreate != null)
            _services.Persistence.Enqueue(new PersistenceTask<Trade>(toCreate) { ActionType = DatabaseActionType.Create });
        if (toUpdate != null)
            _services.Persistence.Enqueue(new PersistenceTask<Trade>(toUpdate.Values.ToList()) { ActionType = DatabaseActionType.Update });
    }

    private async Task CheckAccountAndBalance(User user)
    {
        foreach (var account in user.Accounts)
        {
            var externalAccount = await _services.Portfolio.GetAccountByName(account.Name, true);
            var internalAccount = await _services.Portfolio.GetAccountByName(account.Name);
            if (internalAccount == null && externalAccount != null)
            {
                _log.Warn("Internally stored account is missing; will sync with external one.");
                _services.Persistence.Enqueue(new PersistenceTask<Account>(externalAccount) { ActionType = DatabaseActionType.Create });

            }
            else if (externalAccount != null && externalAccount != internalAccount)
            {
                _log.Warn("Internally stored account does not exactly match the external account; will sync with external one.");
                _services.Persistence.Enqueue(new PersistenceTask<Account>(externalAccount) { ActionType = DatabaseActionType.Update });
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