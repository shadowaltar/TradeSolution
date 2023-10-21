using Autofac.Core;
using Common;
using log4net;
using System.Data;
using System.Linq;
using TradeCommon.Algorithms;
using TradeCommon.Constants;
using TradeCommon.Database;
using TradeCommon.Essentials.Accounts;
using TradeCommon.Essentials.Instruments;
using TradeCommon.Essentials.Portfolios;
using TradeCommon.Essentials.Trading;
using TradeCommon.Runtime;
using TradeDataCore.Essentials;
using TradeLogicCore.Algorithms;
using TradeLogicCore.Maintenance;
using TradeLogicCore.Services;

namespace TradeLogicCore;
public class Core
{
    private static readonly ILog _log = Logger.New();

    private readonly Dictionary<long, IAlgorithmEngine> _engines = new();
    private readonly IServices _services;
    private readonly IdGenerator _assetIdGenerator;
    private Reconcilation? _reconcilation;

    public IReadOnlyDictionary<long, IAlgorithmEngine> Engines => _engines;
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
    public async Task<long> Run(AlgorithmParameters parameters, Algorithm algorithm)
    {
        _log.Info($"Starting algorithm: {algorithm.GetType().Name}, Id [{algorithm.Id}], VerId [{algorithm.VersionId}]");
        var user = _services.Admin.CurrentUser;
        if (user == null) throw new InvalidOperationException("The user does not exist.");

        var startTime = parameters.TimeRange.ActualStartTime;
        if (!startTime.IsValid()) throw new InvalidOperationException("The start time is incorrect.");

        var isExternalAvailable = await _services.Admin.Ping();
        if (!isExternalAvailable) throw Exceptions.Unreachable(Context.Broker);
        // some externals need this for calculation like minimal notional amount
        var refPrices = await _services.MarketData.GetPrices(parameters.SecurityPool);
        SetMinQuantities(refPrices);

        _reconcilation = new Reconcilation(Context);

        await ReconcileAccount(user);
        await ReconcileAssets();

        // check one week's historical order / trade only
        var previousDay = startTime.AddMonths(-1);
        await ReconcileOrders(previousDay, parameters.SecurityPool);
        await ReconcileTrades(previousDay, parameters.SecurityPool);
        await _reconcilation.ReconcilePositions(parameters.SecurityPool);

        // load all open positions, related trades and orders
        // plus all open orders
        await PrepareCaches();

        var uniqueId = Context.AlgoBatchId;
        _ = Task.Factory.StartNew(async () =>
        {
            var engineParameters = new EngineParameters(true, false, true);
            var engine = new AlgorithmEngine(Context, engineParameters);
            _engines[uniqueId] = engine;

            engine.Initialize(algorithm);
            await engine.Run(parameters); // this is a blocking call

        }, TaskCreationOptions.LongRunning);

        // the engine execution is a blocking call
        return uniqueId;
    }

    /// <summary>
    /// Given min notional and ref price, find securities' min quantity.
    /// </summary>
    /// <param name="refPrices"></param>
    private void SetMinQuantities(Dictionary<string, decimal>? refPrices)
    {
        if (refPrices.IsNullOrEmpty()) return;
        foreach (var (code, price) in refPrices)
        {
            var security = _services.Security.GetSecurity(code);
            if (security == null || price == 0) continue;
            security.MinQuantity = security.MinNotional / price;
        }
    }

    private async Task PrepareCaches()
    {
        _services.Order.Reset();
        _services.Trade.Reset();
        _services.Portfolio.Reset(true, true, true);

        var positions = await Context.Storage.ReadPositions(DateUtils.TMinus(Consts.LookbackDayCount), OpenClose.OpenOnly);
        _services.Security.Fix(positions);
        var assets = await Context.Storage.ReadAssets();
        _services.Security.Fix(assets);

        List<Trade> trades = new();
        foreach (var position in positions)
        {
            trades.AddRange(await Context.Storage.ReadTradesByPositionId(position.Security, position.Id, OperatorType.Equals));
        }
        _services.Security.Fix(trades);

        List<Order> orders = new();
        foreach (var group in trades.GroupBy(t => t.Security))
        {
            orders.AddRange(await Context.Storage.ReadOrders(group.Key, group.Select(t => t.OrderId).ToList()));
        }
        _services.Security.Fix(orders);

        _services.Order.Update(orders);
        _services.Trade.Update(trades);
        _services.Portfolio.Update(assets, true);
        _services.Portfolio.Update(positions, true);
    }

    public async Task<ResultCode> StopAlgorithm(long algoSessionId)
    {
        if (_engines.TryGetValue(algoSessionId, out var engine))
        {
            _log.Info("Stopping Algorithm Engine " + algoSessionId);
            await engine.Stop();
            _log.Info("Stopped Algorithm Engine " + algoSessionId);
            _engines.Remove(algoSessionId);
            return ResultCode.StopEngineOk;
        }
        else
        {
            _log.Warn("Failed to stop Algorithm Engine " + algoSessionId);
            return ResultCode.StopEngineFailed;
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

    private async Task ReconcileOrders(DateTime start, List<Security> securities)
    {
        if (securities.IsNullOrEmpty() || start > DateTime.UtcNow) return;

        // sync external to internal
        foreach (var security in securities)
        {
            var internalResults = await _services.Order.GetStorageOrders(security, start);
            var externalResults = await _services.Order.GetExternalOrders(security, start);
            var externalOrders = externalResults.ToDictionary(o => o.ExternalOrderId, o => o);
            var internalOrders = internalResults.ToDictionary(o => o.ExternalOrderId, o => o);

            var (toCreate, toUpdate, toDelete) = Common.CollectionExtensions.FindDifferences(externalOrders, internalOrders, (e, i) => e.EqualsIgnoreId(i));
            if (!toCreate.IsNullOrEmpty())
            {
                _services.Order.Update(toCreate);
                _log.Info($"{toCreate.Count} recent orders for [{security.Id},{security.Code}] are created from external to internal.");
                _log.Info($"Orders [ExternalOrderId][InternalOrderId]:\n\t" + string.Join("\n\t", toCreate.Select(t => $"[{t.ExternalOrderId}][{t.Id}]")));
                foreach (var order in toCreate)
                {
                    order.Comment = "Upserted by reconcilation.";
                    var table = DatabaseNames.GetOrderTableName(order.Security.Type);

                    if (internalOrders.TryGetValue(order.ExternalOrderId, out var conflict))
                    {
                        // an order with the same eoid but different id exists; delete it first
                    }

                    // use upsert, because it is possible for an external order which has the same id vs internal, but with different values
                    await Context.Storage.InsertOne(order, true, tableNameOverride: table);
                }
            }
            if (!toUpdate.IsNullOrEmpty())
            {
                var orders = toUpdate.Values.OrderBy(o => o.Id).ToList();
                var excludeUpdateEOIds = new List<string>();
                foreach (var eoid in toUpdate.Keys)
                {
                    var i = internalOrders[eoid];
                    var e = externalOrders[eoid];
                    var report = Utils.ReportComparison(i, e);
                    foreach (var reportEntry in report.Values)
                    {
                        if (!reportEntry.isEqual)
                        {
                            switch (reportEntry.propertyName)
                            {
                                case nameof(Order.CreateTime):
                                    var minCreateTime = DateUtils.Min((DateTime)reportEntry.value2!, (DateTime)reportEntry.value1!);
                                    i.CreateTime = minCreateTime;
                                    e.CreateTime = minCreateTime;
                                    break;
                                case nameof(Order.UpdateTime):
                                    var maxUpdateTime = DateUtils.Max((DateTime)reportEntry.value2!, (DateTime)reportEntry.value1!);
                                    i.UpdateTime = maxUpdateTime;
                                    e.UpdateTime = maxUpdateTime;
                                    break;
                                case nameof(Order.Price):
                                    if (reportEntry.value2.Equals(0m) && !reportEntry.value1.Equals(0m))
                                        e.Price = (decimal)reportEntry.value1;
                                    break;
                                case nameof(Order.Comment):
                                    if (((string)reportEntry.value2).IsBlank() && !((string)reportEntry.value1).IsBlank())
                                        e.Comment = (string)reportEntry.value1;
                                    break;
                                case nameof(Order.AdvancedSettings):
                                    if (reportEntry.value2 == null && reportEntry.value1 != null)
                                        e.AdvancedSettings = (AdvancedOrderSettings)reportEntry.value1;
                                    break;
                            }
                        }
                    }
                }
                (_, toUpdate, _) = Common.CollectionExtensions.FindDifferences(externalOrders, internalOrders, (e, i) => e.EqualsIgnoreId(i));
                if (!toUpdate.IsNullOrEmpty())
                {
                    orders = toUpdate.Values.OrderBy(o => o.Id).ToList();
                    _services.Order.Update(orders);
                    _log.Info($"{orders.Count} recent orders for [{security.Id},{security.Code}] are updated from external to internal.");
                    _log.Info($"Orders [ExternalOrderId][InternalOrderId]:\n\t" + string.Join("\n\t", orders.Select(t => $"[{t.ExternalOrderId}][{t.Id}]")));
                    foreach (var order in orders)
                    {
                        order.Comment = "Updated by reconcilation.";

                        if (internalOrders.TryGetValue(order.ExternalOrderId, out var conflict) && conflict.Id != order.Id)
                        {
                            // an order with the same eoid but different id exists; move to error
                            await Context.Storage.MoveToError(conflict);
                        }
                    }
                    _services.Persistence.Insert(orders);
                }
            }
            if (!toDelete.IsNullOrEmpty())
            {
                var orders = toDelete.Select(i => internalOrders[i]).ToList();
                _log.Info($"{toDelete.Count} recent orders for [{security.Id},{security.Code}] are moved to error table.");
                _log.Info($"Orders [ExternalOrderId][InternalOrderId]:\n\t" + string.Join("\n\t", orders.Select(t => $"[{t.ExternalOrderId}][{t.Id}]")));
                foreach (var i in toDelete)
                {
                    var order = internalResults.FirstOrDefault(o => o.ExternalOrderId == i);
                    if (order != null)
                    {
                        // order is not successfully sent, we should move it to error orders table
                        await Context.Storage.MoveToError(order);
                    }
                }
            }

            _services.Persistence.WaitAll();
        }
    }

    private async Task ReconcileTrades(DateTime start, List<Security> securities)
    {
        _log.Info($"Reconciling internal vs external recent trades for account {Context.Account?.Name} for broker {Context.Broker} in environment {Context.Environment}.");

        if (securities.IsNullOrEmpty() || start > DateTime.UtcNow) return;
        foreach (var security in securities)
        {
            // must get internal first then external: the external ones will have the corresponding trade id assigned
            var internalResults = await _services.Trade.GetStorageTrades(security, start);
            var externalResults = await _services.Trade.GetExternalTrades(security, start);
            var externalTrades = externalResults.ToDictionary(o => o.ExternalTradeId, o => o);
            var internalTrades = internalResults.ToDictionary(o => o.ExternalTradeId, o => o);

            var missingPositionIdTrades = internalResults.Where(t => t.PositionId <= 0).ToList();
            foreach (var trade in missingPositionIdTrades)
            {
                _log.Warn($"Trade {trade.Id} has no position id, will be fixed in position reconcilation step.");
            }

            // sync external to internal
            var (toCreate, toUpdate, toDelete) = Common.CollectionExtensions.FindDifferences(externalTrades, internalTrades, (e, i) => e.EqualsIgnoreId(i));

            if (!toCreate.IsNullOrEmpty())
            {
                _services.Trade.Update(toCreate, security);
                _log.Info($"{toCreate.Count} recent trades for [{security.Id},{security.Code}] are created from external to internal.");
                _log.Info($"Trades [ExternalTradeId][ExternalOrderId][InternalTradeId][InternalOrderId]:\n\t" + string.Join("\n\t", toCreate.Select(t => $"[{t.ExternalTradeId}][{t.ExternalOrderId}][{t.Id}][{t.OrderId}]")));
                foreach (var trade in toCreate)
                {
                    var table = DatabaseNames.GetTradeTableName(trade.Security.Type);
                    await Context.Storage.InsertOne(trade, false, tableNameOverride: table);
                }
            }
            if (!toUpdate.IsNullOrEmpty())
            {
                var trades = toUpdate.Values;
                _services.Trade.Update(trades, security);
                _log.Info($"{toUpdate.Count} recent trades for [{security.Id},{security.Code}] are updated from external to internal.");
                _log.Info($"Trades [ExternalTradeId][ExternalOrderId][InternalTradeId][InternalOrderId]:\n\t" + string.Join("\n\t", trades.Select(t => $"[{t.ExternalTradeId}][{t.ExternalOrderId}][{t.Id}][{t.OrderId}]")));
                foreach (var trade in trades)
                {
                    var table = DatabaseNames.GetTradeTableName(trade.Security.Type);
                    await Context.Storage.InsertOne(trade, true, tableNameOverride: table);
                }
            }
            if (!toDelete.IsNullOrEmpty())
            {
                var trades = toDelete.Select(i => internalTrades[i]).ToList();
                _log.Info($"{toDelete} recent trades for [{security.Id},{security.Code}] are moved to error table.");
                _log.Info($"Trades [ExternalTradeId][ExternalOrderId][InternalTradeId][InternalOrderId]:\n\t" + string.Join("\n\t", trades.Select(t => $"[{t.ExternalTradeId}][{t.ExternalOrderId}][{t.Id}][{t.OrderId}]")));
                foreach (var trade in trades)
                {
                    if (trade != null)
                    {
                        // malformed trade
                        var tableName = DatabaseNames.GetOrderTableName(trade.Security.SecurityType, true);
                        var r = await Context.Storage.MoveToError(trade);
                    }
                }
            }

            _services.Persistence.WaitAll();
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
                await Context.Storage.InsertOne(externalAccount, true);

            }
            else if (externalAccount != null && !externalAccount.Equals(account))
            {
                _log.Warn("Internally stored account does not exactly match the external account; will sync with external one.");
                await Context.Storage.InsertOne(externalAccount, true);
            }
        }
    }

    private async Task ReconcileAssets()
    {
        var internalResults = await _services.Portfolio.GetStorageAssets();
        var externalResults = await _services.Portfolio.GetExternalAssets();

        // fill in missing fields before comparison
        var assetsNotRegistered = new List<Asset>();
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
            _log.Info($"{i} recent assets for account {Context.Account.Name} are in external but not internal system and are inserted into database.");
        }
        if (!toUpdate.IsNullOrEmpty())
        {
            var assets = toUpdate.Values.OrderBy(a => a.SecurityCode).ToList();
            var excludeUpdateCodes = new List<string>();
            foreach (var code in toUpdate.Keys)
            {
                var ic = internalAssets[code];
                var ec = externalAssets[code];
                var isQuantityEquals = false;
                var isLockedQuantityEquals = false;
                var report = Utils.ReportComparison(ic, ec);
                foreach (var reportEntry in report.Values)
                {
                    switch (reportEntry.propertyName)
                    {
                        case nameof(Asset.Quantity):
                            if (decimal.Equals((decimal)reportEntry.value2, (decimal)reportEntry.value1))
                                isQuantityEquals = true;
                            break;
                        case nameof(Asset.LockedQuantity):
                            if (decimal.Equals((decimal)reportEntry.value2, (decimal)reportEntry.value1))
                                isLockedQuantityEquals = true;
                            break;
                    }
                }
                if (isQuantityEquals && isLockedQuantityEquals)
                    excludeUpdateCodes.Add(code);
            }
            foreach (var code in excludeUpdateCodes)
            {
                toUpdate.Remove(code);
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
                _log.Info($"{i} recent assets for account {Context.Account.Name} from external are different from which in internal system and are updated into database.");
            }
        }
    }
}