using Microsoft.AspNetCore.Mvc;
using TradeDataCore.Database;
using TradeDataCore.Essentials;
using TradeDataCore.StaticData;
using TradeDataCore.Utils;

namespace TradePort.Controllers;

[ApiController]
[Route("admin")]
public class AdminController : Controller
{
    /// <summary>
    /// WARNING, this will erase all the data. Rebuild all the tables.
    /// </summary>
    /// <returns></returns>
    [HttpGet("rebuild-static-tables")]
    public async Task<ActionResult> RebuildStaticTables([FromQuery(Name = "password")] string password)
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
    /// WARNING, this will erase all the data. Rebuild all the tables.
    /// </summary>
    /// <returns></returns>
    [HttpGet("rebuild-price-tables")]
    public async Task<ActionResult> RebuildPriceTables([FromQuery(Name = "password")] string password)
    {
        if (password.IsBlank()) return BadRequest();
        if (!Credential.IsPasswordCorrect(password)) return BadRequest();

        await Storage.CreatePriceTable(IntervalType.OneHour, SecurityType.Equity);
        await Storage.CreatePriceTable(IntervalType.OneDay, SecurityType.Equity);
        await Storage.CreatePriceTable(IntervalType.OneHour, SecurityType.Fx);
        await Storage.CreatePriceTable(IntervalType.OneDay, SecurityType.Fx);

        var tuples = new (string table, string db)[]
        {
            (DatabaseNames.StockPrice1hTable, DatabaseNames.MarketData),
            (DatabaseNames.StockPrice1dTable, DatabaseNames.MarketData),
            (DatabaseNames.FxPrice1hTable, DatabaseNames.MarketData),
            (DatabaseNames.FxPrice1dTable, DatabaseNames.MarketData),
        };
        var results = await Task.WhenAll(tuples.Select(async t =>
        {
            var (table, db) = t;
            var r = await Storage.CheckTableExists(table, db);
            return (table, r);
        }));
        return Ok(results.ToDictionary(p => p.table, p => p.r));
    }
}
