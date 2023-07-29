using Common;
using Microsoft.AspNetCore.Mvc;
using TradeCommon.Constants;
using TradeCommon.Database;
using TradeCommon.Essentials;
using TradeCommon.Essentials.Instruments;
using TradeDataCore.StaticData;

namespace TradePort.Controllers;

/// <summary>
/// Provides admin tasks access.
/// </summary>
[ApiController]
[Route("admin")]
public class AdminController : Controller
{
    /// <summary>
    /// WARNING, this will erase all the data. Rebuild all the tables.
    /// </summary>
    /// <returns></returns>
    [HttpPost("rebuild-static-tables")]
    public async Task<ActionResult> RebuildStaticTables([FromForm] string password)
    {
        if (password.IsBlank()) return BadRequest();
        if (!Credential.IsPasswordCorrect(password)) return BadRequest();

        await Storage.CreateSecurityTable(SecurityType.Equity);
        await Storage.CreateSecurityTable(SecurityType.Fx);
        await Storage.CreateFinancialStatsTable();

        var tuples = new (string table, string db)[]
        {
            (DatabaseNames.StockDefinitionTable, DatabaseNames.StaticData),
            (DatabaseNames.FxDefinitionTable, DatabaseNames.StaticData),
            (DatabaseNames.FinancialStatsTable, DatabaseNames.StaticData),
        };
        var results = await Task.WhenAll(tuples.Select(async t =>
        {
            var (table, db) = t;
            var r = await Storage.CheckTableExists(table, db);
            return (table, r);
        }));
        return Ok(results.ToDictionary(p => p.table, p => p.r));
    }

    /// <summary>
    /// WARNING, this will erase all the data. Rebuild all the price tables.
    /// </summary>
    /// <param name="password">Mandatory</param>
    /// <param name="intervalStr">Must be used along with <paramref name="secTypeStr"/>. Only supports 1m, 1h or 1d. If not set, all will be rebuilt.</param>
    /// <param name="secTypeStr">Must be used along with <paramref name="intervalStr"/>.</param>
    /// <returns></returns>
    [HttpPost("rebuild-price-tables")]
    public async Task<ActionResult> RebuildPriceTables([FromForm] string password,
        [FromQuery(Name = "interval")] string? intervalStr,
        [FromQuery(Name = "sec-type")] string? secTypeStr)
    {
        if (password.IsBlank()) return BadRequest();
        if (!Credential.IsPasswordCorrect(password)) return BadRequest();

        IntervalType interval = IntervalType.Unknown;
        SecurityType secType = SecurityType.Unknown;
        if (intervalStr != null)
            interval = IntervalTypeConverter.Parse(intervalStr);
        if (secTypeStr != null)
            secType = SecurityTypeConverter.Parse(secTypeStr);

        if (interval == IntervalType.Unknown)
        {
            var tuples = new (string table, string db, IntervalType interval, SecurityType secType)[]
            {
                (DatabaseNames.StockPrice1mTable, DatabaseNames.MarketData, IntervalType.OneMinute, SecurityType.Equity),
                (DatabaseNames.StockPrice1hTable, DatabaseNames.MarketData, IntervalType.OneHour, SecurityType.Equity),
                (DatabaseNames.StockPrice1dTable, DatabaseNames.MarketData, IntervalType.OneDay, SecurityType.Equity),
                (DatabaseNames.FxPrice1mTable, DatabaseNames.MarketData, IntervalType.OneMinute, SecurityType.Fx),
                (DatabaseNames.FxPrice1hTable, DatabaseNames.MarketData, IntervalType.OneHour, SecurityType.Fx),
                (DatabaseNames.FxPrice1dTable, DatabaseNames.MarketData, IntervalType.OneDay, SecurityType.Fx),
            };
            var results = await Task.WhenAll(tuples.Select(async t =>
            {
                var (table, db, interval, secType) = t;
                await Storage.CreatePriceTable(interval, secType);
                var r = await Storage.CheckTableExists(table, db);
                return (table, r);
            }));
            return Ok(results.ToDictionary(p => p.table, p => p.r));
        }
        else if (interval is IntervalType.OneMinute or IntervalType.OneHour or IntervalType.OneDay)
        {
            var table = DatabaseNames.GetPriceTableName(interval, secType);
            await Storage.CreatePriceTable(interval, secType);
            var r = await Storage.CheckTableExists(table, DatabaseNames.MarketData);
            return Ok(new Dictionary<string, bool> { { table, r } });
        }

        return BadRequest($"Invalid parameter combination: {intervalStr}, {secTypeStr}");
    }

    /// <summary>
    /// WARNING, this will erase all the data. Rebuild tables with specific type.
    /// </summary>
    /// <param name="password"></param>
    /// <param name="secTypeStr"></param>
    /// <param name="tableTypeStr">Order, Trade or Position.</param>
    /// <returns></returns>
    [HttpPost("rebuild-tables")]
    public async Task<ActionResult> RebuildTables([FromForm] string password,
        [FromQuery(Name = "sec-type")] string? secTypeStr = "equity",
        [FromQuery(Name = "table-type")] string? tableTypeStr = "order")
    {
        if (password.IsBlank()) return BadRequest();
        if (!Credential.IsPasswordCorrect(password)) return BadRequest();
        SecurityType secType = SecurityType.Unknown;
        if (secTypeStr != null)
            secType = SecurityTypeConverter.Parse(secTypeStr);
        DataType dataType = DataType.Unknown;
        if (tableTypeStr != null)
            dataType = DataTypeConverter.Parse(tableTypeStr);

        if (secType is SecurityType.Equity or SecurityType.Fx)
        {
            List<string> resultTableNames = null;
            switch (dataType)
            {
                case DataType.Order:
                    resultTableNames = await Storage.CreateOrderTable(secType);
                    break;
                case DataType.Trade:
                    resultTableNames = await Storage.CreateTradeTable(secType);
                    break;
                case DataType.Position:
                    resultTableNames = await Storage.CreatePositionTable(secType);
                    break;
            }
            if (resultTableNames == null)
                return BadRequest($"Invalid parameters: {tableTypeStr}");

            var results = new Dictionary<string, bool>();
            foreach (var tn in resultTableNames)
            {
                var r = await Storage.CheckTableExists(tn, DatabaseNames.ExecutionData);
                results[tn] = r;
            }
            return Ok(results);
        }

        return BadRequest($"Invalid parameters: {secTypeStr}");
    }

    /// <summary>
    /// WARNING, this will erase all the data. Rebuild all trade tables.
    /// </summary>
    /// <param name="password"></param>
    /// <param name="secTypeStr"></param>
    /// <returns></returns>
    [HttpPost("rebuild-trade-tables")]
    public async Task<ActionResult> RebuildTradeTables([FromForm] string password,
        [FromQuery(Name = "sec-type")] string? secTypeStr)
    {
        if (password.IsBlank()) return BadRequest();
        if (!Credential.IsPasswordCorrect(password)) return BadRequest();
        SecurityType secType = SecurityType.Unknown;
        if (secTypeStr != null)
            secType = SecurityTypeConverter.Parse(secTypeStr);

        if (secType is SecurityType.Equity or SecurityType.Fx)
        {
            var table = DatabaseNames.GetTradeTableName(secType);
            await Storage.CreateTradeTable(secType);
            var r = await Storage.CheckTableExists(table, DatabaseNames.ExecutionData);
            return Ok(new Dictionary<string, bool> { { table, r } });
        }

        return BadRequest($"Invalid parameters: {secTypeStr}");
    }
}
