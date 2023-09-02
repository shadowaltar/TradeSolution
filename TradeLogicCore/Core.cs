using Autofac;
using Common;
using log4net;
using TradeCommon.Constants;
using TradeCommon.Database;
using TradeCommon.Essentials.Accounts;
using TradeCommon.Essentials.Instruments;
using TradeCommon.Essentials.Trading;
using TradeCommon.Runtime;
using TradeLogicCore.Algorithms;
using TradeLogicCore.Algorithms.Parameters;
using TradeLogicCore.Services;

namespace TradeLogicCore;
public class Core
{
    private static readonly ILog _log = Logger.New();

    private readonly Dictionary<Guid, IAlgorithmEngine> _engines = new();
    private readonly Dictionary<Guid, AlgoMetaInfo> _algorithms = new();
    private readonly IServices _services;

    public IReadOnlyDictionary<Guid, IAlgorithmEngine> Engines => _engines;
    public ExchangeType Exchange => Context.Exchange;
    public BrokerType Broker => Context.Broker;
    public EnvironmentType Environment => Context.Environment;
    public Context Context { get; }

    public Core(Context context, IServices services)
    {
        Context = context;
        _services = services;
    }

    /// <summary>
    /// Start a trading algorithm working thread and returns a GUID.
    /// The working thread will not end by itself unless being stopped manually or reaching its designated end time.
    /// 
    /// The following parameters need to be provided:
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
    public async Task<Guid> StartAlgorithm<T>(AlgoStartupParameters parameters, IAlgorithm<T> algorithm) where T : IAlgorithmVariables
    {
        var user = _services.Admin.CurrentUser;
        if (user == null) throw new InvalidOperationException("The user does not exist.");

        var startTime = parameters.TimeRange.ActualStartTime;
        if (!startTime.IsValid()) throw new InvalidOperationException("The start time is incorrect.");

        await ReconcileAccountAndBalance(user);
        await ReconcileOpenOrders(parameters.SecurityPool);

        await Task.Run(async () =>
        {
            // check one day's historical order / trade only
            // they should not impact trading
            var previousDay = startTime.AddDays(-1);
            await ReconcileRecentOrderHistory(previousDay, startTime, parameters.SecurityPool);
            await ReconcileRecentTradeHistory(previousDay, startTime, parameters.SecurityPool);
        });

        var guid = Guid.NewGuid();
        _ = Task.Factory.StartNew(async () =>
        {
            var engine = new AlgorithmEngine<T>(_services, algorithm);
            _engines[guid] = engine;
            await engine.Run(parameters); // this is a blocking call
        }, TaskCreationOptions.LongRunning);

        _algorithms[guid] = new AlgoMetaInfo(guid, algorithm.GetType().Name, parameters);
        // the engine execution is a blocking call
        return guid;
    }

    public async Task StopAlgorithm(Guid guid)
    {
        if (_engines.TryGetValue(guid, out var engine))
        {
            _log.Info("Stopping Algorithm Engine " + guid.ToString());
            await engine.Stop();
            _log.Info("Stopped Algorithm Engine " + guid.ToString());
            _engines.Remove(guid);
            _algorithms.Remove(guid);
        }
        else
        {
            _log.Warn("Failed to stop Algorithm Engine " + guid.ToString());
        }
    }

    public async Task StopAllAlgorithms()
    {
        foreach (var guid in _engines.Keys.ToList())
        {
            await StopAlgorithm(guid);
        }
        _log.Info("All Algorithm Engines are stopped.");
    }

    public List<AlgoMetaInfo> ListAllAlgorithmInfo()
    {
        return _algorithms.Values.ToList();
    }

    private async Task ReconcileRecentOrderHistory(DateTime start, DateTime end, List<Security> securities)
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

    private async Task ReconcileRecentTradeHistory(DateTime start, DateTime end, List<Security> securities)
    {
        var externalTrades = new Dictionary<long, Trade>();
        var internalTrades = new Dictionary<long, Trade>();
        foreach (var security in securities)
        {
            var externalResults = await _services.Trade.GetTrades(security, start, end, true);
            if (externalResults != null)
            {
                foreach (var r in externalResults)
                {
                    if (r == null) continue;
                    externalTrades[r.ExternalTradeId] = r;
                }
            }

            var internalResults = await _services.Trade.GetTrades(security, start, end);
            if (internalResults != null)
            {
                foreach (var r in internalResults)
                {
                    if (r == null) continue;
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

    private async Task ReconcileAccountAndBalance(User user)
    {
        foreach (var account in user.Accounts)
        {
            var externalAccount = await _services.Admin.GetAccount(account.Name, account.Environment, true);
            if (account == null && externalAccount != null)
            {
                _log.Warn("Internally stored account is missing; will sync with external one.");
                _services.Persistence.Enqueue(new PersistenceTask<Account>(externalAccount) { ActionType = DatabaseActionType.Create });

            }
            else if (externalAccount != null && externalAccount != account)
            {
                _log.Warn("Internally stored account does not exactly match the external account; will sync with external one.");
                _services.Persistence.Enqueue(new PersistenceTask<Account>(externalAccount) { ActionType = DatabaseActionType.Update });
            }
        }
    }

    private async Task ReconcileOpenOrders(List<Security> securities)
    {
        //foreach (var security in securities)
        {
            var externalOpenOrders = await _services.Order.GetOpenOrders(null, true);
            var internalOpenOrders = await _services.Order.GetOpenOrders();

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