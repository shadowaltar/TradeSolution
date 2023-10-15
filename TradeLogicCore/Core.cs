using Common;
using log4net;
using System.Data;
using TradeCommon.Algorithms;
using TradeCommon.Constants;
using TradeCommon.Database;
using TradeCommon.Essentials.Accounts;
using TradeCommon.Essentials.Instruments;
using TradeCommon.Essentials.Portfolios;
using TradeCommon.Essentials.Trading;
using TradeCommon.Runtime;
using TradeLogicCore.Algorithms;
using TradeLogicCore.Services;

namespace TradeLogicCore;
public class Core
{
    private static readonly ILog _log = Logger.New();

    private readonly Dictionary<long, IAlgorithmEngine> _engines = new();
    private readonly IServices _services;
    private readonly IdGenerator _assetIdGenerator;

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
    public async Task<long> StartAlgorithm(AlgorithmParameters parameters, IAlgorithm algorithm)
    {
        _log.Info($"Starting algorithm: {algorithm.GetType().Name}, Id [{algorithm.Id}], VerId [{algorithm.VersionId}]");
        var user = _services.Admin.CurrentUser;
        if (user == null) throw new InvalidOperationException("The user does not exist.");

        var startTime = parameters.TimeRange.ActualStartTime;
        if (!startTime.IsValid()) throw new InvalidOperationException("The start time is incorrect.");

        var isExternalAvailable = await _services.Admin.Ping();

        await ReconcileAccount(user);
        await ReconcileAssets(Context.Account);
        //await ReconcileOpenOrders();

        // check one week's historical order / trade only
        var previousDay = startTime.AddMonths(-1);
        await ReconcileOrders(previousDay, parameters.SecurityPool);
        await ReconcileTrades(previousDay, parameters.SecurityPool);
        await ReconcilePositions(parameters.SecurityPool);

        // load all open positions, related trades and orders
        // plus all open orders
        Context.Services.Order.ClearCachedClosedPositionOrders();
        Context.Services.Trade.ClearCachedClosedPositionTrades();
        Context.Services.Portfolio.ClearCachedClosedPositions(true);

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
                _log.Info($"{toCreate.Count} recent orders for [{security.Id},{security.Code}] are in external but not internal system and need to be inserted into database.");
                foreach (var order in toCreate)
                {
                    order.Comment = "Upserted by reconcilation.";
                    var table = DatabaseNames.GetOrderTableName(order.Security.Type);
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
                                    if ((DateTime)reportEntry.value2! > (DateTime)reportEntry.value1!)
                                        e.CreateTime = (DateTime)reportEntry.value1;
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
                    _log.Info($"{orders.Count} recent orders for [{security.Id},{security.Code}] in external are different from internal system and need to be updated into database.");
                    foreach (var order in orders)
                    {
                        order.Comment = "Updated by reconcilation.";
                    }
                    var table = DatabaseNames.GetOrderTableName(orders[0].Security.Type);
                    await Context.Storage.InsertMany(orders, true, tableNameOverride: table);
                }
            }
            if (!toDelete.IsNullOrEmpty())
            {
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

            // read again if necessary and cache them
            if (!toCreate.IsNullOrEmpty() || !toUpdate.IsNullOrEmpty() || !toDelete.IsNullOrEmpty())
            {
                var orders = await _services.Order.GetStorageOrders(security, start);
                _services.Order.Update(orders);
            }
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
            if (!toDelete.IsNullOrEmpty())
            {
                foreach (var i in toDelete)
                {
                    var trade = internalResults.FirstOrDefault(t => t.ExternalTradeId == i);
                    if (trade != null)
                    {
                        // malformed trade
                        var tableName = DatabaseNames.GetOrderTableName(trade.Security.SecurityType, true);
                        var r = await Context.Storage.MoveToError(trade);
                    }
                }
            }

            _services.Persistence.WaitAll();

            // read again if necessary and cache them
            if (!toCreate.IsNullOrEmpty() || !toUpdate.IsNullOrEmpty() || !toDelete.IsNullOrEmpty())
            {
                var trades = _services.Trade.GetTrades(security, start);
                _services.Trade.Update(trades, security);
            }
        }
    }

    /// <summary>
    /// Use all trades information to deduct if position records are correct.
    /// </summary>
    /// <returns></returns>
    private async Task ReconcilePositions(List<Security> securities)
    {
        if (securities.IsNullOrEmpty()) return;

        // dependency: trades
        // case 1: find out all trades without position id, create/update positions.
        // case 2: check if there are trades with incorrect position id (very rare).
        var positions = new List<Position>();
        foreach (var security in securities)
        {
            var ps = await FixZeroPositionIds(security);
            if (ps != null)
                positions.AddRange(ps);
            // assuming all zero pid trades are now fixed...

            // case 2 trades without valid pid (aka position record is somehow missing)
            // have to specify a retrospective date range
            ps = await FixInvalidPositionIdInTrades(security, 7);
            if (ps != null)
                positions.AddRange(ps);
        }

        if (!positions.IsNullOrEmpty())
            _services.Portfolio.UpdatePortfolio(positions, true);

        _services.Persistence.WaitAll();
        return;


        async Task<List<Position>?> FixZeroPositionIds(Security security)
        {
            var (tradeTable, tradeDb) = DatabaseNames.GetTableAndDatabaseName<Trade>(security.SecurityType);
            var (posTable, posDb) = DatabaseNames.GetTableAndDatabaseName<Position>(security.SecurityType);
            if (tradeTable.IsBlank() || posTable.IsBlank()) throw Exceptions.NotImplemented($"Security type {security.SecurityType} is not supported.");

            // #1 fix missing pid trades
            var whereClause = $"SecurityId = {security.Id} AND PositionId = 0 AND AccountId = {Context.AccountId}";
            var trades = await Context.Storage.Read<Trade>(tradeTable, tradeDb, whereClause);
            if (trades.Count > 0)
            {
                // it is possible that a trade with no position exists among other good ones
                // so, find the previous good trade with position, get its position id,
                // then find out the earliest trade with this position id,
                // then from this trade we reconstruct all positions.
                var sql = $@"
SELECT MIN(Id) FROM fx_trades WHERE PositionId = (
	SELECT PositionId FROM (
		SELECT Max(Id), PositionId FROM fx_trades WHERE Id < (
			SELECT MIN(Id) FROM fx_trades WHERE SecurityId = {security.Id} AND PositionId = 0 AND AccountId = {Context.AccountId}
		)
	)
)";
                if (!Context.Storage.TryReadScalar<long>(sql, tradeDb, out var minId))
                {
                    // it means the very first trade in trades table has zero pid
                    minId = trades.Min(t => t.Id);
                }

                whereClause = $"SecurityId = {security.Id} AND Id >= {minId} AND AccountId = {Context.AccountId}";
                trades = await Context.Storage.Read<Trade>(tradeTable, tradeDb, whereClause);
                if (trades.IsNullOrEmpty()) // highly impossible
                    return null; // no historical trades at all

                var ps = await ProcessAndSavePositionAndRecord(security, trades)!;
                _services.Persistence.WaitAll();
                _log.Info($"Position Reconciliation for {security.Code}, trades exist, position record not exist: reconstruct all {ps.Count} positions.");
                return ps;
            }
            return null;
        }

        async Task<List<Position>?> FixInvalidPositionIdInTrades(Security security, int lookbackDays)
        {
            var (tradeTable, tradeDb) = DatabaseNames.GetTableAndDatabaseName<Trade>(security.SecurityType);
            var (posTable, posDb) = DatabaseNames.GetTableAndDatabaseName<Position>(security.SecurityType);
            if (tradeTable.IsBlank() || posTable.IsBlank()) throw Exceptions.NotImplemented($"Security type {security.SecurityType} is not supported.");

            var earliestTradeTime = DateTime.UtcNow.AddDays(-lookbackDays);
            var whereClause = $"SecurityId = {security.Id} AND Time >= '{earliestTradeTime:yyyy-MM-dd HH:mm:ss}' AND AccountId = {Context.AccountId}";
            var trades = await Context.Storage.Read<Trade>(tradeTable, tradeDb, whereClause);
            var positionIds = trades.Select(t => t.PositionId).Distinct().ToList(); // we don't expect zero pid here anymore
            var positionInClause = Storage.GetInClause("Id", positionIds, false);
            var positions = await Context.Storage.Read<Position>(posTable, posDb, $"SecurityId = {security.Id} AND AccountId = {Context.AccountId} AND {positionInClause}");
            var missingPIds = new List<long>();
            foreach (var pidInTrade in positionIds)
            {
                var expectedPos = positions.FirstOrDefault(p => p.Id == pidInTrade);
                if (expectedPos == null)
                    missingPIds.Add(pidInTrade);
            }
            if (missingPIds.Count > 0)
            {
                var smallestMissingPId = missingPIds.Min();
                var tradeWithSmallestId = trades.Where(t => t.PositionId == smallestMissingPId).MinBy(t => t.Id);
                whereClause = $"SecurityId = {security.Id} AND Id >= {tradeWithSmallestId!.Id} AND AccountId = {Context.AccountId}";
                trades = await Context.Storage.Read<Trade>(tradeTable, tradeDb, whereClause);

                var ps = await ProcessAndSavePositionAndRecord(security, trades)!;
                _services.Persistence.WaitAll();
                _log.Info($"Position Reconciliation for {security.Code}, trades exist, position record not exist: reconstruct all {ps.Count} positions.");
                return ps;
            }
            return null;
        }

        async Task<List<Position>> ProcessAndSavePositionAndRecord(Security security, List<Trade> trades)
        {
            // trades may generate more than one position
            List<Position> positions = new();
            foreach (var position in Position.CreateOrApply(trades))
            {
                _services.Security.Fix(position, security);
                positions.Add(position);
            }

            if (positions.IsNullOrEmpty()) throw Exceptions.Impossible("Non-empty list of trades must generate at least one new position.");

            // all non-last positions must be closed!
            for (int i = 0; i < positions.Count - 1; i++)
            {
                Position position = positions[i] ?? throw Exceptions.Impossible("Impossible to hit a null position.");
                if (!position.IsClosed) throw Exceptions.Impossible("Positions generated by trades will always be closed if there are more than one position and it is not the last one.");
            }

            // start order and trade id can uniquely identify a position; remove any if exists
            await RemoveDuplicatedPositions(positions);

            var last = positions[^1];

            // save positions
            var pCnt = await Context.Storage.InsertMany(positions, true);
            LogPositionUpsert(pCnt, security.Code, positions[0].Id, last.Id);

            // save position record
            var rCnt = await Context.Storage.InsertOne(last.CreateRecord(), true);
            LogRecordUpsert(rCnt, security.Code, last.Id);

            // update trades if there is any without position id
            await UpdateTrades(trades);
            return positions;


            async Task UpdateTrades(List<Trade> trades)
            {
                // update all trades (some was zero position id and some
                if (trades.IsNullOrEmpty()) return;

                var database = DatabaseNames.GetDatabaseName<Trade>();
                var sqls = new List<string>();
                foreach (var trade in trades)
                {
                    _services.Security.Fix(trade);
                    var table = DatabaseNames.GetTradeTableName(trade.Security.Type);
                    var sql = $"UPDATE {table} SET PositionId = {trade.PositionId} WHERE Id = {trade.Id}";
                    sqls.Add(sql);
                }
                var count = await Context.Storage.RunMany(sqls, database);
                if (count == trades.Count)
                {
                    _log.Info($"Upsert {count} trades for security {trades[0].SecurityCode} during position reconcilation.");
                }
                else
                {
                    _log.Error($"Failed to upsert {trades.Count - count} trades for security {trades[0].SecurityCode} during position reconcilation.");
                }
            }

            async Task RemoveDuplicatedPositions(List<Position> positions)
            {
                var deleteDuplicationSqls = new List<string>();
                var db = DatabaseNames.GetDatabaseName<Position>();
                foreach (var position in positions)
                {
                    var table = DatabaseNames.GetPositionTableName(position.Security.Type);
                    deleteDuplicationSqls.Add($"DELETE FROM {table} WHERE StartOrderId = {position.StartOrderId} AND StartTradeId = {position.StartTradeId}");
                }
                await Context.Storage.RunMany(deleteDuplicationSqls, db);
            }
        }

        void LogPositionUpsert(int insertedCount, string securityCode, long? positionId = null, long? lastPositionId = null)
        {
            if (insertedCount == 1)
            {
                _log.Info($"Upsert a position for security {securityCode}, position id {positionId}.");
            }
            else if (insertedCount > 1)
            {
                _log.Info($"Upsert {insertedCount} positions for security {securityCode}, first~last position id {positionId} ~ {lastPositionId}.");
            }
            else
            {
                _log.Error($"Failed to upsert one or more positions for security {securityCode}.");
            }
        }

        void LogRecordUpsert(int insertedCount, string securityCode, long positionId)
        {
            if (insertedCount > 0)
            {
                _log.Info($"Upsert a position record for security {securityCode}, id {positionId}.");
            }
            else
            {
                _log.Error($"Failed to upsert a position record for security {securityCode}, id {positionId}.");
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
                await Context.Storage.InsertOne(externalAccount, true);

            }
            else if (externalAccount != null && !externalAccount.Equals(account))
            {
                _log.Warn("Internally stored account does not exactly match the external account; will sync with external one.");
                await Context.Storage.InsertOne(externalAccount, true);
            }
        }
    }

    private async Task ReconcileAssets(Account account)
    {
        var internalResults = await _services.Portfolio.GetStorageAssets(account);
        var externalResults = await _services.Portfolio.GetExternalAssets(account);

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
            _log.Info($"{i} recent assets for account {account.Name} are in external but not internal system and are inserted into database.");
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
            _log.Info($"{i} recent assets for account {account.Name} from external are different from which in internal system and are updated into database.");
        }
        // update the cache if anything is saved from external
        if (toCreate.Count != 0 || toUpdate.Count != 0)
        {
            internalResults = await _services.Portfolio.GetStorageAssets(account);
            _services.Portfolio.UpdatePortfolio(internalResults, true);
        }
    }

    /// <summary>
    /// Find out differences of open orders between external vs internal system.
    /// </summary>
    /// <returns></returns>
    private async Task ReconcileOpenOrders()
    {
        _log.Info($"Reconciling internal vs external open orders for account {Context.Account?.Name} for broker {Context.Broker} in environment {Context.Environment}.");
        var externalOpenOrders = await _services.Order.GetExternalOpenOrders();
        var internalOpenOrders = await _services.Order.GetStoredOpenOrders();
        if (externalOpenOrders.IsNullOrEmpty() && internalOpenOrders.IsNullOrEmpty())
            return;

        var externals = externalOpenOrders.ToDictionary(o => o.ExternalOrderId, o => o);
        var internals = internalOpenOrders.ToDictionary(o => o.ExternalOrderId, o => o);

        var (toCreate, toUpdate, toDelete) = Common.CollectionExtensions.FindDifferences(externals, internals, (e, i) => e.EqualsIgnoreId(i));
        if (!toCreate.IsNullOrEmpty())
        {
            _services.Order.Update(toCreate);
            _log.Info($"{toCreate.Count} open orders are in external but not internal system and need to be inserted into database.");
            foreach (var order in toCreate)
            {
                order.Comment = "Upserted by reconcilation.";
                var table = DatabaseNames.GetOrderTableName(order.Security.Type);
                // use upsert, because it is possible for an external order which has the same id vs internal, but with different values
                await Context.Storage.InsertOne(order, true, tableNameOverride: table);
            }
        }
        if (!toUpdate.IsNullOrEmpty())
        {
            var orders = toUpdate.Values.OrderBy(o => o.Id).ToList();
            _services.Order.Update(orders);
            _log.Info($"{orders.Count} open orders in external are different from internal system and need to be updated into database.");
            foreach (var order in orders)
            {
                order.Comment = "Updated by reconcilation.";
            }
            foreach (var os in orders.GroupBy(o => o.Security.SecurityType))
            {
                var table = DatabaseNames.GetOrderTableName(os.Key);
                await Context.Storage.InsertMany(os.ToList(), true, tableNameOverride: table);
            }
        }
        if (!toDelete.IsNullOrEmpty())
        {
            foreach (var i in toDelete)
            {
                var order = internalOpenOrders.FirstOrDefault(o => o.ExternalOrderId == i);
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