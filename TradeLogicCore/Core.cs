using Common;
using Common.Database;
using log4net;
using System;
using System.Data;
using TradeCommon.Constants;
using TradeCommon.Database;
using TradeCommon.Essentials.Accounts;
using TradeCommon.Essentials.Instruments;
using TradeCommon.Essentials.Misc;
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
    private readonly IdGenerator _assetIdGenerator;

    public IReadOnlyDictionary<Guid, IAlgorithmEngine> Engines => _engines;
    public ExchangeType Exchange => Context.Exchange;
    public BrokerType Broker => Context.Broker;
    public EnvironmentType Environment => Context.Environment;
    public Context Context { get; }

    public Core(Context context, IServices services)
    {
        Context = context;
        _services = services;
        _assetIdGenerator = IdGenerators.Get<Asset>();
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
    public async Task<Guid> StartAlgorithm<T>(AlgorithmParameters parameters, IAlgorithm<T> algorithm) where T : IAlgorithmVariables
    {
        var user = _services.Admin.CurrentUser;
        if (user == null) throw new InvalidOperationException("The user does not exist.");

        var startTime = parameters.TimeRange.ActualStartTime;
        if (!startTime.IsValid()) throw new InvalidOperationException("The start time is incorrect.");

        await ReconcileAccount(user);
        await ReconcileAssets(Context.Account);
        await ReconcileOpenOrders();

        // check one week's historical order / trade only
        var previousDay = startTime.AddMonths(-1);
        await ReconcileRecentOrders(previousDay, parameters.SecurityPool);
        await ReconcileRecentTrades(previousDay, parameters.SecurityPool);
        await ReconcilePositions(parameters.SecurityPool);

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

    private async Task ReconcileRecentOrders(DateTime start, List<Security> securities)
    {
        if (securities.IsNullOrEmpty() || start > DateTime.UtcNow) return;

        // sync external to internal
        foreach (var security in securities)
        {
            var externalResults = await _services.Order.GetOrders(security, start, null, true);
            var internalResults = await _services.Order.GetOrders(security, start, null);
            var externalOrders = externalResults.ToDictionary(o => o.ExternalOrderId, o => o);
            var internalOrders = internalResults.ToDictionary(o => o.ExternalOrderId, o => o);

            var (toCreate, toUpdate, toDelete) = Common.CollectionExtensions.FindDifferences(externalOrders, internalOrders, (e, i) => e.EqualsIgnoreId(i));
            if (!toCreate.IsNullOrEmpty())
            {
                _services.Order.Update(toCreate);
                _log.Info($"{toCreate.Count} recent orders for [{security.Id},{security.Code}] are in external but not internal system and need to be inserted into database.");
                foreach (var order in toCreate)
                {
                    var table = DatabaseNames.GetOrderTableName(order.Security.Type);
                    await Context.Storage.InsertOne(order, false, tableNameOverride: table);
                }
            }
            if (!toUpdate.IsNullOrEmpty())
            {
                _services.Order.Update(toUpdate.Values);
                _log.Info($"{toUpdate.Count} recent orders for [{security.Id},{security.Code}] in external are different from internal system and need to be updated into database.");
                foreach (var order in toUpdate.Values)
                {
                    var table = DatabaseNames.GetOrderTableName(order.Security.Type);
                    await Context.Storage.InsertOne(order, true, tableNameOverride: table);
                }
            }

            // read again if necessary and cache them
            if (toCreate.IsNullOrEmpty() && toUpdate.IsNullOrEmpty() && toDelete.IsNullOrEmpty())
            {
                _services.Order.Update(internalOrders.Values);
            }
            else
            {
                var orders = await _services.Order.GetOrders(security, start, null);
                _services.Order.Update(orders);
            }
        }
    }

    private async Task ReconcileRecentTrades(DateTime start, List<Security> securities)
    {
        _log.Info($"Reconciling internal vs external recent trades for account {Context.Account?.Name} for broker {Context.Broker} in environment {Context.Environment}.");

        if (securities.IsNullOrEmpty() || start > DateTime.UtcNow) return;
        foreach (var security in securities)
        {
            var externalResults = await _services.Trade.GetTrades(security, start, null, true);
            var internalResults = await _services.Trade.GetTrades(security, start, null);
            var externalTrades = externalResults.ToDictionary(o => o.ExternalTradeId, o => o);
            var internalTrades = internalResults.ToDictionary(o => o.ExternalTradeId, o => o);

            // sync external to internal
            var (toCreate, toUpdate, toDelete) = Common.CollectionExtensions.FindDifferences(externalTrades, internalTrades, (e, i) => e.EqualsIgnoreId(i));

            if (!toCreate.IsNullOrEmpty())
            {
                _services.Trade.Update(toCreate, security);
                _log.Info($"{toCreate.Count} recent trades for [{security.Id},{security.Code}] are in external but not internal system and need to be inserted into database.");
                foreach (var trade in toCreate)
                {
                    var table = DatabaseNames.GetTradeTableName(trade.Security.Type);
                    await Context.Storage.InsertOne(trade, false, tableNameOverride: table);
                }
            }
            if (!toUpdate.IsNullOrEmpty())
            {
                _services.Trade.Update(toUpdate.Values, security);
                _log.Info($"{toUpdate.Count} recent trades for [{security.Id},{security.Code}] in external are different from internal system and need to be updated into database.");
                foreach (var trade in toUpdate.Values)
                {
                    var table = DatabaseNames.GetTradeTableName(trade.Security.Type);
                    await Context.Storage.InsertOne(trade, true, tableNameOverride: table);
                }
            }

            // read again if necessary and cache them
            if (toCreate.IsNullOrEmpty() && toUpdate.IsNullOrEmpty() && toDelete.IsNullOrEmpty())
            {
                _services.Trade.Update(internalTrades.Values, security);
            }
            else
            {
                var trades = await _services.Trade.GetTrades(security, start, null);
                _services.Trade.Update(trades, security);
            }
        }
    }

    /// <summary>
    /// Find out differences of account and asset asset information between external vs internal system.
    /// </summary>
    /// <param name="user"></param>
    /// <returns></returns>
    private async Task ReconcileAccount(User user)
    {
        _log.Info($"Reconciling internal vs external accounts and asset assets for account {Context.Account?.Name} for broker {Context.Broker} in environment {Context.Environment}.");
        foreach (var account in user.Accounts)
        {
            var externalAccount = await _services.Admin.GetAccount(account.Name, account.Environment, true);
            if (account == null && externalAccount != null)
            {
                _log.Warn("Internally stored account is missing; will sync with external one.");
                _services.Persistence.Enqueue(externalAccount);

            }
            else if (externalAccount != null && !externalAccount.Equals(account))
            {
                _log.Warn("Internally stored account does not exactly match the external account; will sync with external one.");
                _services.Persistence.Enqueue(externalAccount);
            }
        }
    }

    private async Task ReconcileAssets(Account account)
    {
        var externalResults = await _services.Portfolio.GetAssets(account, true);
        var internalResults = await _services.Portfolio.GetAssets(account);

        // fill in missing fields before comparison
        foreach (var a in externalResults)
        {
            _services.Security.Fix(a);
            a.AccountId = Context.AccountId;
        }
        foreach (var a in internalResults)
        {
            _services.Security.Fix(a);
            a.AccountId = Context.AccountId;
        }

        var externalAssets = externalResults.ToDictionary(a => a.SecurityCode!);
        var internalAssets = internalResults.ToDictionary(a => a.SecurityCode!);
        var (toCreate, toUpdate, toDelete) = Common.CollectionExtensions.FindDifferences(externalAssets, internalAssets,
           (e, i) => e.EqualsIgnoreId(i));
        if (!toCreate.IsNullOrEmpty())
        {
            foreach (var asset in toCreate)
            {
                asset.Id = _assetIdGenerator.NewTimeBasedId;
            }
            var i = await Context.Storage.InsertMany(toCreate, false);
            _log.Info($"{i} recent assets for [{account.Id}] are in external but not internal system and are inserted into database.");
        }
        if (!toUpdate.IsNullOrEmpty())
        {
            // to-update items do not have proper asset id yet
            foreach (var (code, item) in toUpdate)
            {
                var id = internalAssets?.GetOrDefault(code)?.Id;
                if (id == null) throw Exceptions.Impossible();
                item.Id = id.Value;
            }
            var i = await Context.Storage.InsertMany(toUpdate.Values.ToList(), true);
            _log.Info($"{i} recent assets for [{account.Id}] in external are different from internal system and are updated into database.");
        }
        // update the cache if anything is saved from external
        if (toCreate.Count != 0 || toUpdate.Count != 0)
        {
            internalResults = await _services.Portfolio.GetAssets(account);
            _services.Portfolio.Update(internalResults, true);
        }
    }

    /// <summary>
    /// Find out differences of open orders between external vs internal system.
    /// </summary>
    /// <returns></returns>
    private async Task ReconcileOpenOrders()
    {
        _log.Info($"Reconciling internal vs external open orders for account {Context.Account?.Name} for broker {Context.Broker} in environment {Context.Environment}.");
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

    /// <summary>
    /// Use all trades information to deduct if position records are correct.
    /// </summary>
    /// <returns></returns>
    private async Task ReconcilePositions(List<Security> securities)
    {
        _log.Info($"Reconciling internal vs external position entries for account {Context.Account?.Name} for broker {Context.Broker} in environment {Context.Environment}.");
        var records = await Context.Storage.ReadPositionReconciliationRecords(securities.Select(s => s.Id).ToList());


        var positions = new List<Position>();
        foreach (var security in securities)
        {
            List<Trade> trades;
            Position? position = null;
            var lastTime = DateTime.MinValue;
            var lastPositionId = 0L;
            var record = records.FirstOrDefault(r => r.SecurityId == security.Id);
            if (record != null)
            {
                lastPositionId = record.LastPositionId;
                lastTime = record.EndTime;
                // the last time is inclusive
                // there is a chance that the trades are already reconciled but still retrieved here
                trades = await _services.Trade.GetTrades(security, lastTime, null, true);
            }
            else
            {
                trades = await _services.Trade.GetTrades(security, new DateTime(2023, 9, 1), null);
            }
            // sort and filter
            trades = trades.OrderBy(t => t.Time).Where(t => t.Time > lastTime).ToList();
            if (trades.Count == 0)
            {
                // no new trades related to this position after last trade time
                continue;
            }

            if (lastPositionId == 0)
            {
                // case 1, no open position
                position = _services.Portfolio.CreateOrUpdate(trades);
                if (position != null)
                {
                    lastPositionId = position.IsClosed ? 0 : position.Id; // if closed, mark the pid as 0 which means 'there is no open position'
                    lastTime = position.UpdateTime;
                }
            }
            else
            {
                // case 2, there is one open position
                position = _services.Portfolio.GetPositionBySecurityId(security.Id);
                // if it does not exist, we assume it is the reconcilation table goes wrong
                if (position == null)
                {
                    // TODO delete reconcilation entry
                }
                else
                {
                    position = _services.Portfolio.CreateOrUpdate(trades, position);
                    lastPositionId = position!.IsClosed ? 0 : position.Id;
                    lastTime = position?.UpdateTime ?? DateTime.MinValue;
                }
            }

            if (position != null)
            {
                // save reconcilation result
                var r = new PositionReconciliationRecord
                {
                    SecurityId = security.Id,
                    LastPositionId = lastPositionId,
                    EndTime = lastTime,
                };
                var i = await Context.Storage.InsertOne(r, true);
                if (i > 0)
                {
                    _log.Info($"Updated position reconciliation record for security {security.Code}. Last open position id is {(lastPositionId <= 0 ? "nil" : lastPositionId)} at {(lastPositionId <= 0 ? "nil" : lastTime.ToString("yyyyMMdd-HHmmssfff"))}");
                }
                else
                {
                    _log.Info($"Failed to update position reconciliation record for security {security.Code}.");
                }

                positions.Add(position);
            }
        }
        _services.Portfolio.Update(positions);
    }
}