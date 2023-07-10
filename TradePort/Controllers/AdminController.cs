using Microsoft.AspNetCore.Mvc;
using TradeDataCore.Database;
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
    [HttpGet("rebuild-tables")]
    public async Task<ActionResult> RebuildTables([FromQuery(Name = "password")] string password)
    {
        if (password.IsBlank()) return BadRequest();
        if (!Credential.IsPasswordCorrect(password)) return BadRequest();

        await Storage.CreateSecurityTable();
        await Storage.CreatePriceTable();
        await Storage.CreateFinancialStatsTable();

        var tuples = new (string table, string db)[]
        {
            (DatabaseNames.PriceTable, DatabaseNames.MarketData),
            (DatabaseNames.SecurityTable, DatabaseNames.StaticData),
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
}
