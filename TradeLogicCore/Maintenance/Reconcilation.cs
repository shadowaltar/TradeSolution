using Autofac.Core;
using Common;
using log4net;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TradeCommon.Constants;
using TradeCommon.Database;
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
    private readonly Persistence _persistence;
    private readonly IStorage _storage;
    private readonly int _accountId;
    private readonly ISecurityService _securityService;
    private readonly IdGenerator _tradeIdGenerator;

    public Reconcilation(Context context)
    {
        _persistence = context.Services.Persistence;
        _storage = context.Storage;
        _accountId = context.AccountId;
        _securityService = context.Services.Security;
        _tradeIdGenerator = IdGenerators.Get<Trade>();
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
            // validate each position, it may not exist
            await FixInvalidPositions(security, Consts.LookbackDayCount);
        }
        _persistence.WaitAll();
        return;

    }

    private async Task FixOutOfOrderTrades(Security security, int lookbackDays)
    {
        var (tradeTable, tradeDb) = DatabaseNames.GetTableAndDatabaseName<Trade>(security.SecurityType);
        var (posTable, posDb) = DatabaseNames.GetTableAndDatabaseName<Position>(security.SecurityType);
        if (tradeTable.IsBlank() || posTable.IsBlank()) throw Exceptions.NotImplemented($"Security type {security.SecurityType} is not supported.");

        var earliestTradeTime = DateUtils.TMinus(lookbackDays);
        var whereClause = $"SecurityId = {security.Id} AND Time >= '{earliestTradeTime:yyyy-MM-dd HH:mm:ss}' AND AccountId = {_accountId} ORDER BY Time, ExternalTradeId, ExternalOrderId";
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

        whereClause = $"SecurityId = {security.Id} AND ({Storage.GetInClause("StartTradeId", oldIds, false)} OR {Storage.GetInClause("EndTradeId", oldIds, false)})";
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
        await _storage.RunOne($"DELETE FROM {tradeTable} WHERE {Storage.GetInClause("Id", oldIds, false)}", tradeDb);
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
        var whereClause = $"SecurityId = {security.Id} AND Time >= '{earliestTradeTime:yyyy-MM-dd HH:mm:ss}' AND AccountId = {_accountId}";
        var trades = await _storage.Read<Trade>(tradeTable, tradeDb, whereClause);
        var positionIds = trades.Select(t => t.PositionId).Distinct().ToList(); // we don't expect zero pid here anymore
        var positionInClause = Storage.GetInClause("Id", positionIds, false);
        var positions = await _storage.Read<Position>(posTable, posDb, $"SecurityId = {security.Id} AND AccountId = {_accountId} AND {positionInClause}");
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
            whereClause = $"SecurityId = {security.Id} AND Id >= {tradeWithSmallestId!.Id} AND AccountId = {_accountId}";
            trades = await _storage.Read<Trade>(tradeTable, tradeDb, whereClause);

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
        var whereClause = $"SecurityId = {security.Id} AND PositionId = 0 AND AccountId = {_accountId}";
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
			SELECT MIN(Id) FROM fx_trades WHERE SecurityId = {security.Id} AND PositionId = 0 AND AccountId = {_accountId}
		)
	)
)";
            if (!_storage.TryReadScalar<long>(sql, tradeDb, out var minId))
            {
                // it means the very first trade in trades table has zero pid
                minId = trades.Min(t => t.Id);
            }

            whereClause = $"SecurityId = {security.Id} AND Id >= {minId} AND AccountId = {_accountId}";
            trades = await _storage.Read<Trade>(tradeTable, tradeDb, whereClause);
            if (trades.IsNullOrEmpty()) // highly impossible
                return null; // no historical trades at all

            // out of order trade id handling
            // not only reconstruct the trade ids but also may need to update existing positions
            var ps = await ProcessAndSavePosition(security, trades)!;
            _persistence.WaitAll();
            _log.Info($"Position Reconciliation for {security.Code}, reconstruct {ps.Count} positions.");
            return ps;
        }
        return null;
    }

    private async Task FixInvalidPositions(Security security, int lookbackDayCount)
    {
        var (tradeTable, tradeDb) = DatabaseNames.GetTableAndDatabaseName<Trade>(security.SecurityType);
        var (posTable, posDb) = DatabaseNames.GetTableAndDatabaseName<Position>(security.SecurityType);
        if (tradeTable.IsBlank() || posTable.IsBlank()) throw Exceptions.NotImplemented($"Security type {security.SecurityType} is not supported.");

        var positions = await _storage.Read<Position>(posTable, posDb, $"SecurityId = {security.Id} AND AccountId = {_accountId}");
        var errorCount = 0;
        var movedErrorCount = 0;
        foreach (var position in positions)
        {
            var relatedTrades = await _storage.Read<Trade>(tradeTable, tradeDb, $"SecurityId = {security.Id} AND AccountId = {_accountId} AND PositionId = {position.Id}");
            if (relatedTrades.Count == 0)
            {
                errorCount++;
                _securityService.Fix(position);
                var r = await _storage.MoveToError(position);
                if (r != 0)
                    movedErrorCount++;
            }
        }
        _log.Warn($"Found {errorCount} positions with issues and moved {movedErrorCount} entries to error table.");
    }

    private async Task<List<Position>> ProcessAndSavePosition(Security security, List<Trade> trades)
    {
        // trades may generate more than one position
        List<Position> positions = new();
        foreach (var position in Position.CreateOrApply(trades))
        {
            _securityService.Fix(position, security);
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
        var pCnt = await _storage.InsertMany(positions, true);
        LogPositionUpsert(pCnt, security.Code, positions[0].Id, last.Id);

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
