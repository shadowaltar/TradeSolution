using Common;
using Microsoft.AspNetCore.Mvc;
using TradeCommon.Constants;
using TradeCommon.Database;
using TradeCommon.Essentials;
using TradeCommon.Essentials.Accounts;
using TradeCommon.Essentials.Instruments;
using TradeCommon.Runtime;
using TradeDataCore.StaticData;
using TradeLogicCore.Services;

namespace TradePort.Controllers;

/// <summary>
/// Provides admin tasks access.
/// </summary>
[ApiController]
[Route("admin")]
public class AdminController : Controller
{
    /// <summary>
    /// Create a new user.
    /// </summary>
    /// <param name="adminService"></param>
    /// <param name="model"></param>
    /// <param name="userName"></param>
    /// <returns></returns>
    [HttpPost("users/{user}")]
    public async Task<ActionResult> CreateUser([FromServices] IAdminService adminService,
                                               [FromForm] UserCreationModel model,
                                               [FromRoute(Name = "user")] string userName)
    {
        if (userName.IsBlank()) return BadRequest();
        if (model == null) return BadRequest();
        if (model.UserPassword.IsBlank()) return BadRequest();
        if (model.AdminPassword.IsBlank()) return BadRequest();
        if (model.Email.IsBlank() || !model.Email.IsValidEmail()) return BadRequest();
        if (!Credential.IsAdminPasswordCorrect(model.AdminPassword)) return BadRequest();

        if (userName.Length < 3) return BadRequest("User name should at least have 3 chars.");
        if (model.UserPassword.Length < 6) return BadRequest("Password should at least have 6 chars.");

        var user = await adminService.ReadUser(userName, model.Environment);
        if (user != null)
        {
            return BadRequest();
        }

        var result = await adminService.CreateUser(userName, model.Email, model.UserPassword, model.Environment);
        model.UserPassword = "";

        return Ok(result);
    }

    /// <summary>
    /// Create a new account.
    /// </summary>
    /// <param name="adminService"></param>
    /// <param name="model"></param>
    /// <param name="accountName"></param>
    /// <returns></returns>
    [HttpPost("accounts/{accountName}")]
    public async Task<ActionResult> CreateAccount([FromServices] IAdminService adminService,
                                                  [FromForm] AccountCreationModel model,
                                                  [FromRoute] string accountName)
    {
        if (accountName.IsBlank()) return BadRequest();
        if (model == null) return BadRequest();
        if (!Credential.IsAdminPasswordCorrect(model.AdminPassword)) return BadRequest();

        if (accountName.Length < 3) return BadRequest("Account name should at least have 3 chars.");

        var user = await adminService.ReadUser(model.OwnerName, model.Environment);
        if (user == null) return BadRequest("Invalid owner.");
        var brokerId = ExternalNames.GetBrokerId(model.Broker);
        if (brokerId == ExternalNames.BrokerTypeToIds[BrokerType.Unknown]) return BadRequest("Invalid broker.");

        var now = DateTime.UtcNow;
        var account = new Account
        {
            OwnerId = user.Id,
            Name = accountName,
            Type = model.Type,
            SubType = model.SubType,
            Environment = model.Environment,
            BrokerId = brokerId,
            CreateTime = now,
            UpdateTime = now,
            ExternalAccount = model.ExternalAccount,
            FeeStructure = model.FeeStructure,
        };
        var result = await adminService.CreateAccount(account);
        return Ok(result);
    }

    /// <summary>
    /// Get details of a user.
    /// </summary>
    /// <param name="adminService"></param>
    /// <param name="userName"></param>
    /// <param name="environment"></param>
    /// <returns></returns>
    [HttpGet("users/{user}")]
    public async Task<ActionResult> GetUser([FromServices] IAdminService adminService,
                                            [FromRoute(Name = "user")] string userName,
                                            [FromQuery(Name = "environment")] EnvironmentType environment)
    {
        if (userName.IsBlank()) return BadRequest();

        var user = await adminService.ReadUser(userName, environment);
        if (user == null)
        {
            return NotFound();
        }
        return Ok(user);
    }

    /// <summary>
    /// WARNING, this will erase all the data. Rebuild all the tables.
    /// </summary>
    /// <returns></returns>
    [HttpPost("rebuild-static-tables")]
    public async Task<ActionResult> RebuildStaticTables([FromForm] string password)
    {
        if (password.IsBlank()) return BadRequest();
        if (!Credential.IsAdminPasswordCorrect(password)) return BadRequest();

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
        if (!Credential.IsAdminPasswordCorrect(password)) return BadRequest();

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
    /// <param name="tableTypeStr">Order, Trade, Position, FinancialStat, or TOP (trade-order-position-relationship).</param>
    /// <returns></returns>
    [HttpPost("rebuild-tables")]
    public async Task<ActionResult> RebuildTables([FromForm] string password,
        [FromQuery(Name = "sec-type")] string? secTypeStr = "equity",
        [FromQuery(Name = "table-type")] string? tableTypeStr = "order")
    {
        if (password.IsBlank()) return BadRequest();
        if (!Credential.IsAdminPasswordCorrect(password)) return BadRequest();
        SecurityType secType = SecurityType.Unknown;
        if (secTypeStr != null)
            secType = SecurityTypeConverter.Parse(secTypeStr);
        DataType dataType = DataType.Unknown;
        if (tableTypeStr != null)
            dataType = DataTypeConverter.Parse(tableTypeStr);

        List<string>? resultTableNames = null;

        switch (dataType)
        {
            case DataType.FinancialStat:
                await Storage.CreateFinancialStatsTable();
                resultTableNames = new List<string> { DatabaseNames.FinancialStatsTable };
                break;
            case DataType.Account:
                await Storage.CreateAccountTable();
                resultTableNames = new List<string> { DatabaseNames.AccountTable };
                break;
            case DataType.Balance:
                await Storage.CreateBalanceTable();
                resultTableNames = new List<string> { DatabaseNames.BalanceTable };
                break;
            case DataType.User:
                await Storage.CreateUserTable();
                resultTableNames = new List<string> { DatabaseNames.UserTable };
                break;
        }

        if (secType is SecurityType.Equity or SecurityType.Fx)
        {
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
                case DataType.TradeOrderPositionRelationship:
                    resultTableNames = new List<string> { await Storage.CreateTradeOrderPositionIdTable(secType) };
                    break;
            }
            if (resultTableNames == null)
                return BadRequest($"Invalid parameters: {tableTypeStr}");

            var results = new Dictionary<string, bool>();
            foreach (var tn in resultTableNames)
            {
                var r = await Storage.CheckTableExists(tn, DatabaseNames.ExecutionData);
                if (!r)
                    r = await Storage.CheckTableExists(tn, DatabaseNames.StaticData);
                if (!r)
                    r = await Storage.CheckTableExists(tn, DatabaseNames.MarketData);
                results[tn] = r;
            }
            return Ok(results);
        }

        return BadRequest($"Invalid parameters: {secTypeStr}");
    }


    public class UserCreationModel
    {
        [FromForm(Name = "adminPassword")]
        public string AdminPassword { get; set; }

        [FromForm(Name = "userPassword")]
        public string UserPassword { get; set; }

        [FromForm(Name = "email")]
        public string Email { get; set; }

        [FromForm(Name = "environment")]
        public EnvironmentType Environment { get; set; }
    }


    public class AccountCreationModel
    {
        [FromForm(Name = "adminPassword")]
        public string AdminPassword { get; set; }

        [FromForm(Name = "ownerName")]
        public string OwnerName { get; set; }

        [FromForm(Name = "brokerType")]
        public string Broker { get; set; }

        [FromForm(Name = "externalAccount")]
        public string ExternalAccount { get; set; }

        [FromForm(Name = "type")]
        public string Type { get; set; }

        [FromForm(Name = "subType")]
        public string? SubType { get; set; }

        [FromForm(Name = "feeStructure")]
        public string? FeeStructure { get; set; }

        [FromForm(Name = "environment")]
        public EnvironmentType Environment { get; set; }
    }
}