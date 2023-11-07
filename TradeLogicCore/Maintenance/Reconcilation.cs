using Common;
using log4net;
using TradeCommon.Constants;
using TradeCommon.Database;
using TradeCommon.Essentials.Accounts;
using TradeCommon.Essentials.Instruments;
using TradeCommon.Essentials.Portfolios;
using TradeCommon.Essentials.Trading;
using TradeCommon.Runtime;
using TradeDataCore.Instruments;
using TradeLogicCore.Services;

namespace TradeLogicCore.Maintenance;

public class Reconcilation
{
    private static readonly ILog _log = Logger.New();
    private readonly Context _context;
    private readonly Persistence _persistence;
    private readonly IStorage _storage;
    private readonly ISecurityService _securityService;
    private readonly IOrderService _orderService;
    private readonly ITradeService _tradeService;
    private readonly IAdminService _adminService;
    private readonly IPortfolioService _portfolioService;
    private readonly IdGenerator _tradeIdGenerator;
    private readonly IdGenerator _assetIdGenerator;

    public Reconcilation(Context context)
    {
        _context = context;
        _persistence = context.Services.Persistence;
        _storage = context.Storage;
        _securityService = context.Services.Security;
        _orderService = context.Services.Order;
        _tradeService = context.Services.Trade;
        _adminService = context.Services.Admin;
        _portfolioService = context.Services.Portfolio;
        _tradeIdGenerator = IdGenerators.Get<Trade>();
        _assetIdGenerator = IdGenerators.Get<Asset>();
    }

    /// <summary>
    /// Use all trades information to deduct if position records are correct.
    /// </summary>
    /// <returns></returns>
    public async Task ReconcilePositions(List<Security> securities)
    {
        if (securities.IsNullOrEmpty()) return;

        // dependency: trades
        // case 1: find out all trades without position id, create/update positions.
        // case 2: check if there are trades with incorrect position id (very rare).
        var positions = new List<Position>();
        foreach (var security in securities)
        {
            // case 1, some trades are out of order
            // regenerate ids and fill into related positions
            await FixOutOfOrderTrades(security, Consts.LookbackDayCount);

            // case 2, some trades (especially those just inserted during reconciliation) has no position id
            // get earliest affected trade and reconstruct all positions
            var ps = await FixZeroPositionIds(security);
            if (ps != null)
                positions.AddRange(ps);

            // assuming all zero pid trades are now fixed...

            // case 3 trades without valid pid (aka position record is somehow missing)
            ps = await FixInvalidPositionIdInTrades(security, Consts.LookbackDayCount);
            if (ps != null)
                positions.AddRange(ps);

            // assuming all trades have their positions inserted

            // case 4 positions do not align with trades
            // validate each position, it may not be a valid one
            await FixInvalidPositions(security, Consts.LookbackDayCount);

            // case 5 positions are manually removed from storage
            // validate each trade, it may not have position
            //await FixMissingPositions(security, Consts.LookbackDayCount);
        }
        _persistence.WaitAll();
        return;
    }

    public async Task ReconcileOrders(DateTime start, List<Security> securities)
    {
        if (securities.IsNullOrEmpty() || start > DateTime.UtcNow) return;

        // sync external to internal
        foreach (var security in securities)
        {
            var internalResults = await _orderService.GetStorageOrders(security, start);
            var externalResults = await _orderService.GetExternalOrders(security, start);
            var externalOrders = externalResults.ToDictionary(o => o.ExternalOrderId, o => o);
            var internalOrders = internalResults.ToDictionary(o => o.ExternalOrderId, o => o);

            var (toCreate, toUpdate, toDelete) = Common.CollectionExtensions.FindDifferences(externalOrders, internalOrders, (e, i) => e.EqualsIgnoreId(i));
            if (!toCreate.IsNullOrEmpty())
            {
                _orderService.Update(toCreate);
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
                    await _storage.InsertOne(order, true, tableNameOverride: table);
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
                    foreach (var (propertyName, isEqual, value1, value2) in report.Values)
                    {
                        if (!isEqual)
                        {
                            switch (propertyName)
                            {
                                case nameof(Order.CreateTime):
                                    var minCreateTime = DateUtils.Min((DateTime)value2!, (DateTime)value1!);
                                    i.CreateTime = minCreateTime;
                                    e.CreateTime = minCreateTime;
                                    break;
                                case nameof(Order.UpdateTime):
                                    var maxUpdateTime = DateUtils.Max((DateTime)value2!, (DateTime)value1!);
                                    i.UpdateTime = maxUpdateTime;
                                    e.UpdateTime = maxUpdateTime;
                                    break;
                                case nameof(Order.Price):
                                    if (value2.Equals(0m) && !value1.Equals(0m))
                                        e.Price = (decimal)value1;
                                    break;
                                case nameof(Order.Comment):
                                    if (((string)value2).IsBlank() && !((string)value1).IsBlank())
                                        e.Comment = (string)value1; // orders' comment only appears internally
                                    break;
                                case nameof(Order.Action):
                                    if ((OrderActionType)value2 != (OrderActionType)value1)
                                        e.Action = (OrderActionType)value1; // orders' action type only appears internally
                                    break;
                                case nameof(Order.AdvancedSettings):
                                    if (value2 == null && value1 != null)
                                        e.AdvancedSettings = (AdvancedOrderSettings)value1;
                                    // orders' adv settings only appears internally
                                    break;
                            }
                        }
                    }
                }
                (_, toUpdate, _) = Common.CollectionExtensions.FindDifferences(externalOrders, internalOrders, (e, i) => e.EqualsIgnoreId(i));
                if (!toUpdate.IsNullOrEmpty())
                {
                    orders = toUpdate.Values.OrderBy(o => o.Id).ToList();
                    _orderService.Update(orders);
                    _log.Info($"{orders.Count} recent orders for [{security.Id},{security.Code}] are updated from external to internal.");
                    _log.Info($"Orders [ExternalOrderId][InternalOrderId]:\n\t" + string.Join("\n\t", orders.Select(t => $"[{t.ExternalOrderId}][{t.Id}]")));
                    foreach (var order in orders)
                    {
                        order.Comment = "Updated by reconcilation.";

                        if (internalOrders.TryGetValue(order.ExternalOrderId, out var conflict) && conflict.Id != order.Id)
                        {
                            // an order with the same eoid but different id exists; move to error
                            await _storage.MoveToError(conflict);
                        }
                    }
                    _persistence.Insert(orders);
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
                        await _storage.MoveToError(order);
                    }
                }
            }

            // add the initial items of order states if it was empty
            foreach (var order in externalResults)
            {
                var (table, _) = DatabaseNames.GetTableAndDatabaseName<OrderState>(order.Security.SecurityType);
                var i = await _storage.Count<OrderState>(tableNameOverride: table, $"OrderId = {order.Id}");
                if (i == 0)
                {
                    var state = OrderState.From(order);
                    await _storage.InsertOne(state, false);
                }
            }

            _persistence.WaitAll();
        }
    }

    public async Task ReconcileTrades(DateTime start, List<Security> securities)
    {
        _log.Info($"Reconciling internal vs external recent trades for account {_context.Account.Name} for broker {_context.Broker} in environment {_context.Environment}.");

        if (securities.IsNullOrEmpty() || start > DateTime.UtcNow) return;
        foreach (var security in securities)
        {
            // must get internal first then external: the external ones will have the corresponding trade id assigned
            var internalResults = await _tradeService.GetStorageTrades(security, start, isOperational: null);
            var externalResults = await _tradeService.GetExternalTrades(security, start);
            var externalTrades = externalResults.ToDictionary(o => o.ExternalTradeId, o => o);
            var internalTrades = internalResults.ToDictionary(o => o.ExternalTradeId, o => o);

            var missingPositionIdTrades = internalResults.Where(t => t.PositionId <= 0).ToList();
            foreach (var trade in missingPositionIdTrades)
            {
                if (!trade.IsOperational)
                    _log.Warn($"Trade {trade.Id} has no position id, will be fixed in position reconcilation step.");
            }

            // sync external to internal
            var (toCreate, toUpdate, toDelete) = Common.CollectionExtensions.FindDifferences(externalTrades, internalTrades, (e, i) => e.EqualsIgnoreId(i));

            if (!toCreate.IsNullOrEmpty())
            {
                // there may be correct order which already exist
                // so reassign the order id in these trades
                foreach (var group in toCreate.GroupBy(t => t.ExternalOrderId))
                {
                    var referenceOrder = _orderService.GetOrderByExternalId(group.Key);
                    if (referenceOrder != null)
                    {
                        foreach (var trade in group)
                        {
                            trade.OrderId = referenceOrder.Id;
                            if (referenceOrder.Action == OrderActionType.Operational)
                                trade.IsOperational = true;
                        }
                    }
                }
                // there may be the same existing trade categorized as 'to-create' but actually just some fields are different
                // use external id to find them
                var (tradeTable, tradeDb) = DatabaseNames.GetTableAndDatabaseName<Trade>(security.SecurityType);
                foreach (var trade in toCreate)
                {
                    var existings = await _storage.Read<Trade>(tradeTable, tradeDb, "ExternalTradeId = " + trade.ExternalTradeId);
                    if (!existings.IsNullOrEmpty())
                    {
                        var existing = existings[0];
                        if (trade.Id != existing.Id)
                        {
                            trade.Id = existing.Id;
                        }
                    }
                }
                _tradeService.Update(toCreate, security);
                _log.Info($"{toCreate.Count} recent trades for [{security.Id},{security.Code}] are created from external to internal.");
                _log.Info(string.Join("\n\t", toCreate.Select(t => $"ID:{t.Id}, ETID:{t.ExternalTradeId}, OID:{t.OrderId}, EOID:{t.ExternalOrderId}")));

                await _storage.InsertMany(toCreate, false);
            }
            if (!toUpdate.IsNullOrEmpty())
            {
                var trades = toUpdate.Values;
                _tradeService.Update(trades, security);
                _log.Info($"{toUpdate.Count} recent trades for [{security.Id},{security.Code}] are updated from external to internal.");
                _log.Info(string.Join("\n\t", trades.Select(t => $"ID:{t.Id}, ETID:{t.ExternalTradeId}, OID:{t.OrderId}, EOID:{t.ExternalOrderId}")));
                foreach (var trade in trades)
                {
                    var table = DatabaseNames.GetTradeTableName(trade.Security.Type);
                    await _storage.InsertOne(trade, true, tableNameOverride: table);
                }
            }
            if (!toDelete.IsNullOrEmpty())
            {
                var trades = toDelete.Select(i => internalTrades[i]).ToList();
                _log.Info($"{toDelete.Count} recent trades for [{security.Id},{security.Code}] are moved to error table.");
                _log.Info(string.Join("\n\t", trades.Select(t => $"ID:{t.Id}, ETID:{t.ExternalTradeId}, OID:{t.OrderId}, EOID:{t.ExternalOrderId}")));
                foreach (var trade in trades)
                {
                    if (trade != null)
                    {
                        // malformed trade
                        var tableName = DatabaseNames.GetOrderTableName(trade.Security.SecurityType, true);
                        var r = await _storage.MoveToError(trade);
                    }
                }
            }

            _persistence.WaitAll();
        }
    }

    /// <summary>
    /// Find out differences of account and asset asset information between external vs internal system.
    /// </summary>
    /// <param name="user"></param>
    /// <returns></returns>
    public async Task ReconcileAccount(User user)
    {
        _log.Info($"Reconciling internal vs external accounts and asset assets for account {_context.Account?.Name} for broker {_context.Broker} in environment {_context.Environment}.");
        foreach (var account in user.Accounts)
        {
            var externalAccount = await _adminService.GetAccount(account.Name, account.Environment, true);
            if (account == null && externalAccount != null)
            {
                _log.Warn("Internally stored account is missing; will sync with external one.");
                await _storage.InsertOne(externalAccount, true);

            }
            else if (externalAccount != null && !externalAccount.Equals(account))
            {
                _log.Warn("Internally stored account does not exactly match the external account; will sync with external one.");
                await _storage.InsertOne(externalAccount, true);
            }
        }
    }

    public async Task ReconcileAssets()
    {
        var internalResults = await _portfolioService.GetStorageAssets();
        var externalResults = await _portfolioService.GetExternalAssets();

        // fill in missing fields before comparison
        var assetsNotRegistered = new List<Asset>();
        foreach (var a in externalResults)
        {
            _securityService.Fix(a);
            a.AccountId = _context.AccountId;
        }
        foreach (var a in internalResults)
        {
            _securityService.Fix(a);
            a.AccountId = _context.AccountId;
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
            var i = await _storage.InsertMany(toCreate, false);
            _log.Info($"{i} recent assets for account {_context.Account.Name} are in external but not internal system and are inserted into database.");
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
                foreach (var (propertyName, isEqual, value1, value2) in report.Values)
                {
                    switch (propertyName)
                    {
                        case nameof(Asset.Quantity):
                            if (decimal.Equals((decimal)value2, (decimal)value1))
                                isQuantityEquals = true;
                            break;
                        case nameof(Asset.LockedQuantity):
                            if (decimal.Equals((decimal)value2, (decimal)value1))
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
                var i = await _storage.InsertMany(toUpdate.Values.ToList(), true);
                _log.Info($"{i} recent assets for account {_context.Account.Name} from external are different from which in internal system and are updated into database.");
            }
        }

        // add the initial batch of asset states if it was empty
        foreach (var asset in internalResults)
        {
            var i = await _storage.Count<AssetState>(whereClause: $"AssetId = {asset.Id}");
            if (i == 0)
            {
                var state = AssetState.From(asset);
                await _storage.InsertOne(state, false);
            }
        }

        _persistence.WaitAll();
    }

    private async Task FixOutOfOrderTrades(Security security, int lookbackDays)
    {
        var (tradeTable, tradeDb) = DatabaseNames.GetTableAndDatabaseName<Trade>(security.SecurityType);
        var (posTable, posDb) = DatabaseNames.GetTableAndDatabaseName<Position>(security.SecurityType);
        if (tradeTable.IsBlank() || posTable.IsBlank()) throw Exceptions.NotImplemented($"Security type {security.SecurityType} is not supported.");

        var earliestTradeTime = DateUtils.TMinus(lookbackDays);
        var whereClause = $"SecurityId = {security.Id} AND Time >= '{earliestTradeTime:yyyy-MM-dd HH:mm:ss}' AND AccountId = {_context.AccountId} AND IsOperational = 0 ORDER BY Time, ExternalTradeId, ExternalOrderId";
        var trades = await _storage.Read<Trade>(tradeTable, tradeDb, whereClause);
        if (trades.Count <= 1) return;

        // find out-of-order trade ids
        // generate good trade ids for them, along with all other trades followed by the out-of-order trades
        // remove the the old ones, insert the new ones
        var oldIds = new List<long>();
        var oldToNewIds = new Dictionary<long, long>(); // mapping in case need to fix positions' start/end tid
        var shouldRegenerateId = false;
        for (int i = 0; i < trades.Count - 1; i++)
        {
            var current = trades[i];
            var next = trades[i + 1];
            if (current.Id > next.Id)
            {
                shouldRegenerateId = true; // once hit, all later trades' ids need to be regenerated
            }
            if (shouldRegenerateId)
            {
                var newId = _tradeIdGenerator.NewTimeBasedId;
                oldToNewIds[next.Id] = newId;
                oldIds.Add(next.Id);
            }
        }
        if (oldIds.IsNullOrEmpty())
            return;

        whereClause = $"SecurityId = {security.Id} AND ({Storage.GetInClause("StartTradeId", oldIds, false, false)} OR {Storage.GetInClause("EndTradeId", oldIds, false, false)})";
        var affectedPositions = await _storage.Read<Position>(posTable, posDb, whereClause);

        var affectedTrades = new List<Trade>();
        foreach (var oldId in oldIds)
        {
            var newId = oldToNewIds[oldId];
            var affectedTrade = trades.First(t => t.Id == oldId);
            affectedTrade.Id = newId;
            _securityService.Fix(affectedTrade);
            affectedTrades.Add(affectedTrade);
        }
        await _storage.RunOne($"DELETE FROM {tradeTable} WHERE {Storage.GetInClause("Id", oldIds, false, false)}", tradeDb);
        await _storage.InsertMany(affectedTrades, false);
        var updateSqls = new List<string>();
        foreach (var ap in affectedPositions)
        {
            var newId = oldToNewIds.GetOrDefault(ap.StartTradeId, 0);
            if (newId != 0)
                updateSqls.Add($"UPDATE {posTable} SET StartTradeId = {newId} WHERE StartTradeId = {ap.StartTradeId}");
            newId = oldToNewIds.GetOrDefault(ap.EndTradeId, 0);
            if (newId != 0)
                updateSqls.Add($"UPDATE {posTable} SET EndTradeId = {newId} WHERE EndTradeId = {ap.StartTradeId}");
        }
        if (!updateSqls.IsNullOrEmpty())
        {
            var r = await _storage.RunMany(updateSqls, posDb);
        }
    }

    private async Task<List<Position>?> FixInvalidPositionIdInTrades(Security security, int lookbackDays)
    {
        var (tradeTable, tradeDb) = DatabaseNames.GetTableAndDatabaseName<Trade>(security.SecurityType);
        var (posTable, posDb) = DatabaseNames.GetTableAndDatabaseName<Position>(security.SecurityType);
        if (tradeTable.IsBlank() || posTable.IsBlank()) throw Exceptions.NotImplemented($"Security type {security.SecurityType} is not supported.");

        var earliestTradeTime = DateUtils.TMinus(lookbackDays);
        var whereClause = $"SecurityId = {security.Id} AND Time >= '{earliestTradeTime:yyyy-MM-dd HH:mm:ss}' AND AccountId = {_context.AccountId} AND IsOperational = 0";
        var trades = await _storage.Read<Trade>(tradeTable, tradeDb, whereClause);
        var positionIds = trades.Select(t => t.PositionId).Distinct().ToList(); // we don't expect zero pid here anymore
        var positionInClause = Storage.GetInClause("Id", positionIds, true, false);
        var positions = await _storage.Read<Position>(posTable, posDb, $"SecurityId = {security.Id} AND AccountId = {_context.AccountId} {positionInClause}");
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
            whereClause = $"SecurityId = {security.Id} AND Id >= {tradeWithSmallestId!.Id} AND AccountId = {_context.AccountId} AND IsOperational = 0";
            trades = await _storage.Read<Trade>(tradeTable, tradeDb, whereClause);
            _securityService.Fix(trades);
            var ps = await ProcessAndSavePosition(security, trades)!;
            _persistence.WaitAll();
            _log.Info($"Position Reconciliation for {security.Code}, reconstruct {ps.Count} positions.");
            return ps;
        }
        return null;
    }

    private async Task<List<Position>?> FixZeroPositionIds(Security security)
    {
        var (tradeTable, tradeDb) = DatabaseNames.GetTableAndDatabaseName<Trade>(security.SecurityType);
        var (posTable, posDb) = DatabaseNames.GetTableAndDatabaseName<Position>(security.SecurityType);
        if (tradeTable.IsBlank() || posTable.IsBlank()) throw Exceptions.NotImplemented($"Security type {security.SecurityType} is not supported.");

        // #1 fix missing pid trades
        var whereClause = $"SecurityId = {security.Id} AND PositionId = 0 AND AccountId = {_context.AccountId} AND IsOperational = 0";
        var trades = await _storage.Read<Trade>(tradeTable, tradeDb, whereClause);
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
			SELECT MIN(Id) FROM fx_trades WHERE SecurityId = {security.Id} AND PositionId = 0 AND AccountId = {_context.AccountId}
		)
	)
)";
            var (isGood, minId) = await _storage.TryReadScalar<long>(sql, tradeDb);
            if (!isGood)
            {
                // it means the very first trade in trades table has zero pid
                minId = trades.Min(t => t.Id);
            }

            whereClause = $"SecurityId = {security.Id} AND Id >= {minId} AND AccountId = {_context.AccountId} AND IsOperational = 0";
            trades = await _storage.Read<Trade>(tradeTable, tradeDb, whereClause);
            if (trades.IsNullOrEmpty()) // highly impossible
                return null; // no historical trades at all

            // out of order trade id handling
            // not only reconstruct the trade ids but also may need to update existing positions
            _securityService.Fix(trades);
            var ps = await ProcessAndSavePosition(security, trades)!;
            _persistence.WaitAll();
            _log.Info($"Position Reconciliation for {security.Code}, reconstruct {ps.Count} positions.");
            return ps;
        }
        return null;
    }

    private async Task FixInvalidPositions(Security security, int lookbackDays)
    {
        var (tradeTable, tradeDb) = DatabaseNames.GetTableAndDatabaseName<Trade>(security.SecurityType);
        var (posTable, posDb) = DatabaseNames.GetTableAndDatabaseName<Position>(security.SecurityType);
        if (tradeTable.IsBlank() || posTable.IsBlank()) throw Exceptions.NotImplemented($"Security type {security.SecurityType} is not supported.");

        var earliestPositionStartTime = DateUtils.TMinus(lookbackDays);
        var positions = await _storage.Read<Position>(posTable, posDb, $"SecurityId = {security.Id} AND AccountId = {_context.AccountId} AND CreateTime >= '{earliestPositionStartTime:yyyy-MM-dd HH:mm:ss}'");
        var errorCount = 0;
        var movedErrorCount = 0;
        foreach (var position in positions)
        {
            var relatedTrades = await _storage.Read<Trade>(tradeTable, tradeDb, $"SecurityId = {security.Id} AND AccountId = {_context.AccountId} AND PositionId = {position.Id} AND IsOperational = 0");
            if (relatedTrades.Count == 0)
            {
                errorCount++;
                _securityService.Fix(position);
                var r = await _storage.MoveToError(position);
                if (r != 0)
                    movedErrorCount++;
            }
        }
        if (errorCount != 0)
            _log.Warn($"Found {errorCount} positions with issues and moved {movedErrorCount} entries to error table.");
    }

    //private async Task FixInvalidPositions(Security security, int lookbackDays)
    //{
    //    var (tradeTable, tradeDb) = DatabaseNames.GetTableAndDatabaseName<Trade>(security.SecurityType);
    //    var (posTable, posDb) = DatabaseNames.GetTableAndDatabaseName<Position>(security.SecurityType);
    //    if (tradeTable.IsBlank() || posTable.IsBlank()) throw Exceptions.NotImplemented($"Security type {security.SecurityType} is not supported.");

    //    var earliestTradeTime = DateUtils.TMinus(lookbackDays);
    //    var trades = await _storage.Read<Position>(posTable, posDb, $"SecurityId = {security.Id} AND AccountId = {_context.AccountId} AND Time >= '{earliestPositionStartTime:yyyy-MM-dd HH:mm:ss}'");
    //    var errorCount = 0;
    //    var movedErrorCount = 0;
    //    foreach (var position in positions)
    //    {
    //        var relatedTrades = await _storage.Read<Trade>(tradeTable, tradeDb, $"SecurityId = {security.Id} AND AccountId = {_context.AccountId} AND PositionId = {position.Id}");
    //        if (relatedTrades.Count == 0)
    //        {
    //            errorCount++;
    //            _securityService.Fix(position);
    //            var r = await _storage.MoveToError(position);
    //            if (r != 0)
    //                movedErrorCount++;
    //        }
    //    }
    //    _log.Warn($"Found {errorCount} positions with issues and moved {movedErrorCount} entries to error table.");
    //}

    private async Task<List<Position>> ProcessAndSavePosition(Security security, List<Trade> trades)
    {
        // trades may generate more than one position
        List<Position> positions = new();
        Position? lastPosition = null;
        for (int i = 0; i < trades.Count; i++)
        {
            Trade? trade = trades[i];
            if (trade.IsOperational) continue;
            lastPosition = _portfolioService.CreateOrApply(trade, lastPosition);
            if (lastPosition != null && (lastPosition.IsClosed || i == trades.Count - 1))
            {
                positions.Add(lastPosition);
                lastPosition = null;
            }
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
        var pCnt = await _storage.InsertMany(positions, true);
        LogPositionUpsert(pCnt, security.Code, positions[0].Id, last.Id);

        // update trades if there is any without position id
        _securityService.Fix(trades);
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
                _securityService.Fix(trade);
                var table = DatabaseNames.GetTradeTableName(trade.Security.Type);
                var sql = $"UPDATE {table} SET PositionId = {trade.PositionId} WHERE Id = {trade.Id}";
                sqls.Add(sql);
            }
            var count = await _storage.RunMany(sqls, database);
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
            await _storage.RunMany(deleteDuplicationSqls, db);
        }
    }

    private static void LogPositionUpsert(int upsertedCount, string securityCode, long? positionId = null, long? lastPositionId = null)
    {
        if (upsertedCount == 1)
        {
            _log.Info($"Upsert a position for security {securityCode}, position id {positionId}.");
        }
        else if (upsertedCount > 1)
        {
            _log.Info($"Upsert {upsertedCount} positions for security {securityCode}, first~last position id {positionId} ~ {lastPositionId}.");
        }
        else
        {
            _log.Error($"Failed to upsert one or more positions for security {securityCode}.");
        }
    }
}
