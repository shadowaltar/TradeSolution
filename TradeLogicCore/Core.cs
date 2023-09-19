using Common;
using log4net;
using OfficeOpenXml.Style;
using TradeCommon.Constants;
using TradeCommon.Database;
using TradeCommon.Essentials.Accounts;
using TradeCommon.Essentials.Instruments;
using TradeCommon.Essentials.Portfolios;
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
        await ReconcileOpenOrders();

        // check one week's historical order / trade only
        var previousDay = startTime.AddDays(-7);
        await CacheAndReconcileRecentOrders(previousDay, startTime, parameters.SecurityPool);
        await CacheAndReconcileRecentTrades(previousDay, startTime, parameters.SecurityPool);

        var guid = Guid.NewGuid();
        _ = Task.Factory.StartNew(async () =>
        {
            var engine = new AlgorithmEngine<T>(Context);
            engine.Initialize(algorithm);

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

    private async Task CacheAndReconcileRecentOrders(DateTime start, DateTime end, List<Security> securities)
    {
        if (securities.IsNullOrEmpty() || start > end) return;
        var externalOrders = new Dictionary<long, Order>();
        var internalOrders = new Dictionary<long, Order>();

        // sync external to internal
        foreach (var security in securities)
        {
            var externalResults = await _services.Order.GetOrders(security, start, end, true);
            var internalResults = await _services.Order.GetOrders(security, start, end);
            externalOrders.AddRange(externalResults.ToDictionary(o => o.ExternalOrderId, o => o));
            internalOrders.AddRange(internalResults.ToDictionary(o => o.ExternalOrderId, o => o));
        }
        var (toCreate, toUpdate, toDelete) = FindDifferences(externalOrders, internalOrders);
        if (!toCreate.IsNullOrEmpty())
        {
            _services.Order.Update(toCreate);
            _log.Info($"{toCreate.Count} recent orders are in external but not internal system and need to be inserted into database.");
            foreach (var order in toCreate)
            {
                await Context.Storage.InsertOrder(order);
            }
        }
        if (!toUpdate.IsNullOrEmpty())
        {
            _services.Order.Update(toUpdate.Values);
            _log.Info($"{toUpdate.Count} recent orders in external are different from internal system and need to be updated into database.");
            foreach (var order in toUpdate.Values)
            {
                await Context.Storage.InsertOrder(order);
            }
        }

        // read again if necessary and cache them
        if (toCreate.IsNullOrEmpty() && toUpdate.IsNullOrEmpty() && toDelete.IsNullOrEmpty())
        {
            _services.Order.Update(internalOrders.Values);
        }
        else
        {
            foreach (var security in securities)
            {
                var orders = await _services.Order.GetOrders(security, start, end);
                _services.Order.Update(orders);
            }
        }
    }

    private async Task CacheAndReconcileRecentTrades(DateTime start, DateTime end, List<Security> securities)
    {
        if (securities.IsNullOrEmpty() || start > end) return;
        var externalTrades = new Dictionary<long, Trade>();
        var internalTrades = new Dictionary<long, Trade>();
        foreach (var security in securities)
        {
            var externalResults = await _services.Trade.GetTrades(security, start, end, true);
            var internalResults = await _services.Trade.GetTrades(security, start, end);
            externalTrades.AddRange(externalResults.ToDictionary(o => o.ExternalTradeId, o => o));
            internalTrades.AddRange(internalResults.ToDictionary(o => o.ExternalTradeId, o => o));
        }

        // sync external to internal
        var (toCreate, toUpdate, toDelete) = FindDifferences(externalTrades, internalTrades);
        if (!toCreate.IsNullOrEmpty())
        {
            _services.Trade.Update(toCreate);
            _log.Info($"{toCreate.Count} recent trades are in external but not internal system and need to be inserted into database.");
            foreach (var trade in toCreate)
            {
                await Context.Storage.InsertTrade(trade);
            }
        }
        if (!toUpdate.IsNullOrEmpty())
        {
            _services.Trade.Update(toUpdate.Values);
            _log.Info($"{toUpdate.Count} recent trades in external are different from internal system and need to be updated into database.");
            foreach (var trade in toUpdate.Values)
            {
                await Context.Storage.InsertTrade(trade);
            }
        }

        // read again if necessary and cache them
        if (toCreate.IsNullOrEmpty() && toUpdate.IsNullOrEmpty() && toDelete.IsNullOrEmpty())
        {
            _services.Trade.Update(internalTrades.Values);
        }
        else
        {
            foreach (var security in securities)
            {
                var trades = await _services.Trade.GetTrades(security, start, end);
                _services.Trade.Update(trades);
            }
        }
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
            else if (externalAccount != null && !externalAccount.Equals(account))
            {
                _log.Warn("Internally stored account does not exactly match the external account; will sync with external one.");
                _services.Persistence.Enqueue(new PersistenceTask<Account>(externalAccount) { ActionType = DatabaseActionType.Update });
                _services.Persistence.Enqueue(new PersistenceTask<Balance>(externalAccount.Balances));
            }
        }
    }

    private async Task ReconcileOpenOrders()
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

    public static (List<TV>, Dictionary<TK, TV>, List<TK>) FindDifferences<TK, TV>(Dictionary<TK, TV> primary, Dictionary<TK, TV> secondary)
        where TV : IComparable<TV>
    {
        var toCreate = new List<TV>();
        var toUpdate = new Dictionary<TK, TV>();
        var toDelete = new List<TK>();
        foreach (var (id, first) in primary)
        {
            if (secondary.TryGetValue(id, out var second))
            {
                if (second.CompareTo(first) != 0)
                    toUpdate[id] = first;
            }
            else
            {
                toCreate.Add(first);
            }
        }
        foreach (var (id, second) in secondary)
        {
            if (primary.TryGetValue(id, out var first))
            {
                if (second.CompareTo(first) != 0)
                    toUpdate[id] = first;
            }
            else
            {
                toDelete.Add(id);
            }
        }
        return (toCreate, toUpdate, toDelete);
    }
}