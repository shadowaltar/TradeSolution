using Common;
using log4net;
using System.Collections.Generic;
using System.Data;
using System.Security;
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

    private readonly Dictionary<string, IAlgorithmEngine> _engines = new();
    private readonly Dictionary<string, AlgoMetaInfo> _algorithms = new();
    private readonly IServices _services;
    private readonly IdGenerator _assetIdGenerator;

    public IReadOnlyDictionary<string, IAlgorithmEngine> Engines => _engines;
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
    public async Task<string> StartAlgorithm<T>(AlgorithmParameters parameters, IAlgorithm<T> algorithm) where T : IAlgorithmVariables
    {
        _log.Info($"Starting algorithm: {algorithm.GetType().Name}, Id [{algorithm.Id}], VerId [{algorithm.VersionId}]");
        var user = _services.Admin.CurrentUser;
        if (user == null) throw new InvalidOperationException("The user does not exist.");

        var startTime = parameters.TimeRange.ActualStartTime;
        if (!startTime.IsValid()) throw new InvalidOperationException("The start time is incorrect.");

        var isExternalAvailable = await _services.Admin.Ping();

        await ReconcileAccount(user);
        await ReconcileAssets(Context.Account);
        await ReconcileOpenOrders();

        // check one week's historical order / trade only
        var previousDay = startTime.AddMonths(-1);
        await ReconcileOrders(previousDay, parameters.SecurityPool);
        await ReconcileTrades(previousDay, parameters.SecurityPool);
        await ReconcilePositions(parameters.SecurityPool);

        var guid = Context.AlgoSessionId;
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

    public async Task StopAlgorithm(string algoSessionId)
    {
        if (_engines.TryGetValue(algoSessionId, out var engine))
        {
            _log.Info("Stopping Algorithm Engine " + algoSessionId);
            await engine.Stop();
            _log.Info("Stopped Algorithm Engine " + algoSessionId);
            _engines.Remove(algoSessionId);
            _algorithms.Remove(algoSessionId);
        }
        else
        {
            _log.Warn("Failed to stop Algorithm Engine " + algoSessionId);
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
                _services.Order.Update(toUpdate.Values);
                _log.Info($"{toUpdate.Count} recent orders for [{security.Id},{security.Code}] in external are different from internal system and need to be updated into database.");
                foreach (var order in toUpdate.Values)
                {
                    order.Comment = "Upserted by reconcilation.";
                    var table = DatabaseNames.GetOrderTableName(order.Security.Type);
                    await Context.Storage.InsertOne(order, true, tableNameOverride: table);
                }
            }
            if (!toDelete.IsNullOrEmpty())
            {
                foreach (var i in toDelete)
                {
                    var order = internalResults.FirstOrDefault(o => o.ExternalOrderId == i);
                    if (order != null && order.Status == OrderStatus.Sending)
                    {
                        // order is not successfully sent, we should move it to error orders table
                        var tableName = DatabaseNames.GetOrderTableName(order.Security.SecurityType, true);
                        var r = await Context.Storage.MoveToError(order);
                    }
                }
            }

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

        // assumption 1: trades are always correctly stored, in correct time sequence!
        // assumption 2: persistence order is trade -> position -> record
        // given a specific security id:
        // case 1:  the record is missing but there are trades
        //          get all trades stored, construct all positions (and record) from scratch.
        // case 2:  record exists,
        //          get trades from record's end time; get last position from record's position id.
        // case a:  if no position,
        //              go to case 1.
        // case b:  if position exists, and some trades has no position id,
        //              usually it means some trades are just added in previous reconcile-trade step and not yet applied to any positions,
        //              locate the earliest newly added trade, then find the most recent position to be merged (or maybe a new position is needed),
        //              reconstruct from this position onwards.
        // case c:  if last trade position's id > last position's id, OR if last position's vs record's position, id > id,
        //              reconstruct from record's position id.
        // case d:  if last position's vs record's position, id == id && (tradeCount != tradeCount || isClosed != isClosed),
        //              reconstruct last position
        // case e:  if last position's vs record's position, id == id && tradeCount == tradeCount && isClosed == isClosed
        //              gracefully check next security
        _log.Info($"Reconciling internal vs external position entries for account {Context.Account?.Name} for broker {Context.Broker} in environment {Context.Environment}.");
        var records = await Context.Storage.ReadPositionRecords(securities.Select(s => s.Id).ToList());
        var positions = new List<Position>();
        // expect one security id has zero or one record
        foreach (var security in securities)
        {
            var record = records.FirstOrDefault(r => r.SecurityId == security.Id);
            DateTime tradeStartTime;
            Position? lastPosition;
            if (record == null)
            {
                tradeStartTime = DateTime.MinValue;
                lastPosition = null;
            }
            else
            {
                tradeStartTime = record.EndTime;
                var positionsLaterThanRecord = await _services.Portfolio.GetStoragePositions(null, record.EndTime);
                // record may point to a position a earlier than last position
                lastPosition = positionsLaterThanRecord.LastOrDefault();
            }
            var trades = await _services.Trade.GetStorageTrades(security, tradeStartTime);
            if (trades.IsNullOrEmpty())
                continue; // no historical trades at all

            var lastTrade = trades.Last();
            var lastNoPositionTrade = trades.FirstOrDefault(t => t.PositionId <= 0);
            // case 1, no record
            if (record == null)
            {
                var ps = await ProcessAndSavePositionAndRecord(security, trades)!;
                _log.Info($"Position Reconciliation for {security.Code}, trades exist, position record not exist: reconstruct all {ps.Count} positions.");
                positions.AddRange(ps);
            }
            else // case 2 record exists
            {
                // case a, no position
                if (lastPosition == null)
                {
                    var ps = await ProcessAndSavePositionAndRecord(security, trades)!;
                    _log.Info($"Position Reconciliation for {security.Code}, trades exist, positions not exist: reconstruct all {ps.Count} positions.");
                    positions.AddRange(ps);
                }
                else
                {
                    // case b, some trades are totally new, reconstruct from the most recent position
                    if (lastNoPositionTrade != null)
                    {
                        var index = trades.IndexOf(lastNoPositionTrade);
                        if (index < 0) throw Exceptions.Impossible();
                        if (index == 0) // so the recorded end time onwards could create new positions directly
                        {
                            var ps = await ProcessAndSavePositionAndRecord(security, trades)!;
                            _log.Info($"Position Reconciliation for {security.Code}, new positions exist: reconstruct {ps.Count} positions.");
                            positions.AddRange(ps);
                        }
                        else
                        {
                            // reconstruct all positions affected (and after) the zero position id trade
                            var previousGoodTrade = trades[index - 1];
                            var affectedGoodPid = previousGoodTrade.PositionId;
                            var start = previousGoodTrade.Time;
                            trades = await Context.Storage.ReadTrades(security, start, DateTime.MaxValue);
                            // if record shows the good pid is closed, exclude it, otherwise include it
                            if (record.IsClosed)
                                trades = trades.Where(t => t.PositionId > affectedGoodPid || t.PositionId == 0).ToList();
                            else
                                trades = trades.Where(t => t.PositionId >= affectedGoodPid || t.PositionId == 0).ToList();
                            foreach (var trade in trades)
                            {
                                _services.Security.Fix(trade);
                            }
                            var ps = await ProcessAndSavePositionAndRecord(security, trades)!;
                            _log.Info($"Position Reconciliation for {security.Code}," +
                                $" trades exist (last trade pid which is affected by zero-position-id trade: {affectedGoodPid})," +
                                $" record exist (pid: {record.PositionId}):" +
                                $" reconstruct {ps.Count} positions with id >= {affectedGoodPid} (this pid will be abandoned).");
                            positions.AddRange(ps);
                        }
                    }
                    else
                    {
                        // now we expect trade's pid >= p's pid >= record's pid
                        if (lastTrade.PositionId < lastPosition.Id || lastPosition.Id < record.PositionId)
                        {
                            throw Exceptions.Impossible("Last trade must be saved before last position; last position must be saved before record");
                        }
                        // case c, trade pid > last pid OR last pid > record pid
                        if (lastTrade.PositionId > lastPosition.Id || lastPosition.Id > record.PositionId)
                        {
                            // will get all the trades on or after record.pid
                            trades = await Context.Storage.ReadTrades(security, record.PositionId);
                            var ps = await ProcessAndSavePositionAndRecord(security, trades)!;
                            _log.Info($"Position Reconciliation for {security.Code}," +
                                $" trades exist (last trade pid: {lastTrade.PositionId})," +
                                $" positions exist (last pid: {lastPosition.Id})," +
                                $" record exist (pid: {record.PositionId}):" +
                                $" reconstruct {ps.Count} positions with id >= {record.PositionId}.");
                            positions.AddRange(ps);
                        }
                        // case d:  if last position's vs record's position, id == id && (tradeCount != tradeCount || isClosed != isClosed),
                        else if (lastPosition.Id == record.PositionId && (lastPosition.TradeCount != record.TradeCount || lastPosition.IsClosed != record.IsClosed))
                        {
                            trades = await Context.Storage.ReadTrades(security, record.PositionId);
                            var ps = await ProcessAndSavePositionAndRecord(security, trades)!;
                            _log.Info($"Position Reconciliation for {security.Code}," +
                                $" trades exist (last trade pid: {lastTrade.PositionId})," +
                                $" positions exist (last pid: {lastPosition.Id})," +
                                $" record exist (pid: {record.PositionId}):" +
                                $" reconstruct {ps.Count} positions with id == {record.PositionId}.");
                            positions.AddRange(ps);
                        }
                        // case x:  if last position's vs record's position, id == id && tradeCount == tradeCount && isClosed == isClosed),
                        else if (lastPosition.Id == record.PositionId && lastPosition.TradeCount == record.TradeCount && lastPosition.IsClosed == record.IsClosed)
                        {
                            _log.Info($"Position Reconciliation for {security.Code}: trades/positions/record are all matched.");
                        }
                        else
                        {
                            throw Exceptions.Impossible("Impossible position reconciliation case.");
                        }
                    }
                }
            }
        }

        if (!positions.IsNullOrEmpty())
            _services.Portfolio.UpdatePortfolio(positions, true);
        return;


        async Task<List<Position>> ProcessAndSavePositionAndRecord(Security security, List<Trade> trades)
        {
            // trades may generate more than one position
            List<Position> positions = new();
            foreach (var position in Position.CreateOrApply(trades))
            {
                if (!_services.Security.Fix(position, security)) throw Exceptions.Impossible();
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
            if (!_services.Security.Fix(a))
            {
                assetsNotRegistered.Add(a);
            }
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