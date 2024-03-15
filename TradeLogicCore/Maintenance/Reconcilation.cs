using Common;
using log4net;
using TradeCommon.Constants;
using TradeCommon.Database;
using TradeCommon.Essentials.Accounts;
using TradeCommon.Essentials.Instruments;
using TradeCommon.Essentials.Portfolios;
using TradeCommon.Essentials.Trading;
using TradeCommon.Runtime;
using TradeDataCore.Importing.Binance;
using TradeDataCore.Instruments;
using TradeLogicCore.Services;

namespace TradeLogicCore.Maintenance;

public class Reconciliation(Context context)
{
    private static readonly ILog _log = Logger.New();
    private readonly Context _context = context;
    private readonly Persistence _persistence = context.Services.Persistence;
    private readonly IStorage _storage = context.Storage;
    private readonly ISecurityService _securityService = context.Services.Security;
    private readonly IOrderService _orderService = context.Services.Order;
    private readonly ITradeService _tradeService = context.Services.Trade;
    private readonly IAdminService _adminService = context.Services.Admin;
    private readonly IPortfolioService _portfolioService = context.Services.Portfolio;
    private readonly IdGenerator _assetIdGenerator = IdGenerators.Get<Asset>();

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

                foreach (var order in toCreate)
                {
                    _log.Info($"OID:{order.Id}, EOID:{order.ExternalOrderId}");
                    order.Comment = "Upserted by reconcilation.";
                    var table = DatabaseNames.GetOrderTableName(order.Security.Type);

                    if (internalOrders.TryGetValue(order.ExternalOrderId, out var conflict))
                    {
                        // an order with the same eoid but different id exists; delete it first
                    }

                    // use upsert, because it is possible for an external order which has the same id vs internal, but with different values
                    await _storage.UpsertOne(order, tableNameOverride: table);
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
                    var report = Common.Utils.ReportComparison(i, e);
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
                    foreach (var order in orders)
                    {
                        _log.Info($"OID:{order.Id}, EOID:{order.ExternalOrderId}");
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
                foreach (var i in toDelete)
                {
                    _log.Info($"OID:{i}");
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
                    await _storage.UpsertOne(state);
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

            //var missingPositionIdTrades = internalResults.Where(t => t.PositionId <= 0).ToList();
            //foreach (var trade in missingPositionIdTrades)
            //{
            //    if (!trade.IsOperational)
            //        _log.Warn($"Trade {trade.Id} has no position id, will be fixed in position reconcilation step.");
            //}

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
                _log.Info(string.Join("\n", toCreate.Select(t => $"ID:{t.Id}, ETID:{t.ExternalTradeId}, OID:{t.OrderId}, EOID:{t.ExternalOrderId}")));

                await _storage.InsertMany(toCreate, false);
            }
            if (!toUpdate.IsNullOrEmpty())
            {
                var trades = toUpdate.Values;
                _tradeService.Update(trades, security);
                _log.Info($"{toUpdate.Count} recent trades for [{security.Id},{security.Code}] are updated from external to internal.");
                _log.Info(string.Join("\n", trades.Select(t => $"ID:{t.Id}, ETID:{t.ExternalTradeId}, OID:{t.OrderId}, EOID:{t.ExternalOrderId}")));
                foreach (var trade in trades)
                {
                    var table = DatabaseNames.GetTradeTableName(trade.Security.Type);
                    await _storage.UpsertOne(trade, tableNameOverride: table);
                }
            }
            if (!toDelete.IsNullOrEmpty())
            {
                var trades = toDelete.Select(i => internalTrades[i]).ToList();
                _log.Info($"{toDelete.Count} recent trades for [{security.Id},{security.Code}] will be moved to error table.");
                _log.Info(string.Join("\n", trades.Select(t => $"ID:{t.Id}, ETID:{t.ExternalTradeId}, OID:{t.OrderId}, EOID:{t.ExternalOrderId}")));
                foreach (var trade in trades)
                {
                    if (trade != null)
                    {
                        // malformed trade, or test-env external system clean up our data
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
            var externalAccount = await _adminService.GetAccount(account.Name, true);
            if (account == null && externalAccount != null)
            {
                _log.Warn("Internally stored account is missing; will sync with external one.");
                await _storage.InsertOne(externalAccount);
            }
            else if (externalAccount != null && !externalAccount.Equals(account))
            {
                _log.Warn("Internally stored account does not exactly match the external account; will sync with external one.");
                await _storage.UpsertOne(externalAccount);
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
            if (a.Security == null)
            {

            }
            a.AccountId = _context.AccountId;
        }
        foreach (var a in internalResults)
        {
            _securityService.Fix(a);
            if (a.Security == null)
            {

            }
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
                var report = Common.Utils.ReportComparison(ic, ec);
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
            var i = await _storage.Count<AssetState>(whereClause: $"{nameof(AssetState.SecurityId)} = {asset.SecurityId}");
            if (i == 0)
            {
                var state = AssetState.From(asset);
                await _storage.InsertOne(state);
            }

        }
        _portfolioService.Update(internalResults, false);

        _persistence.WaitAll();
    }

    public async Task RunAll(User user, DateTime start, List<Security> securityPool)
    {
        if (_context.Broker == BrokerType.Binance)
        {
            var reader = new SecurityDefinitionReader(_storage);
            await reader.ReadAndSave(SecurityType.Crypto);
        }

        await ReconcileAccount(user);
        await ReconcileAssets();

        // check one week's historical order / trade only
        await ReconcileOrders(start, securityPool);
        await ReconcileTrades(start, securityPool);

        // recalculate actual price + filled quantity, must run after reconcile trades
        await RecalculateOrderPrice(start, securityPool);

        //await ReconcilePositions(securityPool);
        _log.Info("Finished reconciliation.");
    }

    private async Task RecalculateOrderPrice(DateTime start, List<Security> securities)
    {
        var changedOrders = new List<Order>();
        foreach (var security in securities)
        {
            var orders = await _orderService.GetStorageOrders(security, start, null, OrderStatuses.Fills);
            var trades = await _tradeService.GetStorageTrades(security, start, null, null);
            var tradesByOrderId = trades.GroupBy(t => t.OrderId);
            foreach (var order in orders)
            {
                var groupedTrades = tradesByOrderId.FirstOrDefault(g => g.Key == order.Id)?.ToList();
                if (groupedTrades.IsNullOrEmpty())
                {
                    _log.Error("A filled order does not have any associated trades! OrderId: " + order.Id);
                    continue;
                }
                var savedPrice = order.Price;
                var savedQuantity = order.FilledQuantity;
                var pricePrecision = savedPrice.GetDecimalPlaces();
                order.Price = decimal.Round(groupedTrades.WeightedAverage(t => t.Price, t => t.Quantity), security.PricePrecision);
                order.FilledQuantity = groupedTrades.Sum(t => t.Quantity);
                if (savedPrice != order.Price || savedQuantity != order.FilledQuantity)
                {
                    changedOrders.Add(order);
                }
                // try to fix order's limit price in case it is empty
                if (order.Type.IsLimit() && order.LimitPrice == 0)
                {
                    order.LimitPrice = order.Price;
                    changedOrders.Add(order);
                }
            }
        }

        if (!changedOrders.IsNullOrEmpty())
        {
            _persistence.Insert(changedOrders);
            _persistence.WaitAll();
        }
    }
}
