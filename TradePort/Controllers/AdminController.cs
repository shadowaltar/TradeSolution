using Common;
using Microsoft.AspNetCore.Mvc;
using TradeCommon.Constants;
using TradeCommon.Database;
using TradeCommon.Essentials;
using TradeCommon.Essentials.Accounts;
using TradeCommon.Essentials.Algorithms;
using TradeCommon.Essentials.Instruments;
using TradeCommon.Runtime;
using TradeDataCore.StaticData;
using TradeLogicCore.Services;
using TradePort.Utils;

namespace TradePort.Controllers;

/// <summary>
/// Provides admin tasks access.
/// </summary>
[ApiController]
[Route("admin")]
public class AdminController : Controller
{
    /// <summary>
    /// Set application environment + login (combination of two other calls).
    /// </summary>
    /// <param name="adminService"></param>
    /// <param name="adminPassword"></param>
    /// <param name="userName"></param>
    /// <param name="accountName"></param>
    /// <param name="password"></param>
    /// <param name="environment"></param>
    /// <param name="exchange"></param>
    /// <returns></returns>
    [HttpPost("login")]
    public async Task<ActionResult> SetEnvironmentAndLogin([FromServices] IAdminService adminService,
                                                           [FromForm(Name = "admin-password")] string adminPassword,
                                                           [FromForm(Name = "user-password")] string password,
                                                           [FromQuery(Name = "user")] string userName,
                                                           [FromQuery(Name = "account-name")] string accountName,
                                                           [FromQuery(Name = "environment")] EnvironmentType environment = EnvironmentType.Test,
                                                           [FromQuery(Name = "exchange")] ExchangeType exchange = ExchangeType.Binance)
    {
        if (ControllerValidator.IsAdminPasswordBad(adminPassword, out var br)) return br;
        if (ControllerValidator.IsUnknown(environment, out br)) return br;
        if (ControllerValidator.IsUnknown(exchange, out br)) return br;

        var broker = ExternalNames.Convert(exchange);
        adminService.Initialize(environment, exchange, broker);

        var result = await adminService.Login(userName, password, accountName, adminService.Context.Environment);
        return result != ResultCode.LoginUserAndAccountOk
            ? BadRequest($"Failed to {nameof(SetEnvironmentAndLogin)}; code: {result}")
            : Ok(result);
    }

    ///// <summary>
    ///// Set application environment.
    ///// </summary>
    ///// <param name="adminService"></param>
    ///// <param name="adminPassword"></param>
    ///// <param name="environment"></param>
    ///// <param name="exchange"></param>
    ///// <returns></returns>
    //[HttpPost("set-environment")]
    //public ActionResult SetEnvironment([FromServices] IAdminService adminService,
    //                                   [FromForm(Name = "admin-password")] string adminPassword,
    //                                   [FromQuery(Name = "environment")] EnvironmentType environment = EnvironmentType.Test,
    //                                   [FromQuery(Name = "exchange")] ExchangeType exchange = ExchangeType.Binance)
    //{
    //    if (ControllerValidator.IsAdminPasswordBad(adminPassword, out var br)) return br;
    //    if (ControllerValidator.IsUnknown(environment, out br)) return br;
    //    if (ControllerValidator.IsUnknown(exchange, out br)) return br;

    //    adminService.Initialize(environment, exchange, ExternalNames.Convert(exchange));
    //    return Ok(environment);
    //}

    ///// <summary>
    ///// Login.
    ///// </summary>
    ///// <param name="adminService"></param>
    ///// <param name="userName"></param>
    ///// <param name="password"></param>
    ///// <param name="accountName"></param>
    ///// <returns></returns>
    //[HttpPost("login")]
    //public async Task<ActionResult> Login([FromServices] IAdminService adminService,
    //                                      [FromQuery(Name = "user")] string userName,
    //                                      [FromQuery(Name = "account-name")] string accountName,
    //                                      [FromForm(Name = "user-password")] string password)
    //{
    //    var result = await adminService.Login(userName, password, accountName, adminService.Context.Environment);
    //    return result != ResultCode.LoginUserAndAccountOk
    //        ? BadRequest($"Failed to {nameof(Login)}; code: {result}")
    //        : Ok(new Dictionary<string, object?> { { "user", adminService.CurrentUser }, { "account", adminService.CurrentAccount } });
    //}

    /// <summary>
    /// Get details of a user.
    /// </summary>
    /// <param name="adminService"></param>
    /// <param name="userName"></param>
    /// <returns></returns>
    [HttpGet("users/{user}")]
    public async Task<ActionResult> GetUser([FromServices] IAdminService adminService,
                                            [FromRoute(Name = "user")] string userName)
    {
        if (userName.IsBlank()) return BadRequest();

        var user = await adminService.GetUser(userName, adminService.Context.Environment);
        return user == null ? NotFound() : Ok(user);
    }

    /// <summary>
    /// Get account's information.
    /// </summary>
    /// <param name="adminService"></param>
    /// <param name="accountName"></param>
    /// <param name="requestExternal"></param>
    /// <returns></returns>
    [HttpGet("accounts/{account}")]
    public async Task<ActionResult> GetAccount([FromServices] IAdminService adminService,
                                               [FromRoute(Name = "account")] string accountName = "test",
                                               [FromQuery(Name = "request-external")] bool requestExternal = false)
    {
        var account = await adminService.GetAccount(accountName, adminService.Context.Environment, requestExternal);
        return account == null ? BadRequest("Invalid account name.") : Ok(account);
    }

    /// <summary>
    /// Get current login account's information.
    /// </summary>
    /// <param name="adminService"></param>
    /// <returns></returns>
    [HttpGet("accounts/current")]
    public ActionResult GetLoggedInAccount([FromServices] IAdminService adminService)
    {
        return Ok(adminService.CurrentAccount);
    }

    /// <summary>
    /// Get account's information.
    /// </summary>
    /// <param name="adminService"></param>
    /// <param name="accountName"></param>
    /// <param name="requestExternal"></param>
    /// <returns></returns>
    [HttpPost("accounts/{account}/sync")]
    public async Task<ActionResult> SynchronizeAccountAndBalanceFromExternal([FromServices] IAdminService adminService,
                                               [FromRoute(Name = "account")] string accountName = "test")
    {
        var account = await adminService.GetAccount(accountName, adminService.Context.Environment);
        if (account == null) return BadRequest("Invalid account name.");

        var external = await adminService.GetAccount(accountName, adminService.Context.Environment, true);

        return Ok(account);
    }

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

        var user = await adminService.GetUser(userName, model.Environment);
        if (user != null)
        {
            return BadRequest();
        }

        var result = await adminService.CreateUser(userName, model.UserPassword, model.Email, model.Environment);
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
    [HttpPost("accounts/{account}")]
    public async Task<ActionResult> CreateAccount([FromServices] IAdminService adminService,
                                                  [FromForm] AccountCreationModel model,
                                                  [FromRoute(Name = "account")] string accountName = "test")
    {
        if (ControllerValidator.IsAdminPasswordBad(model.AdminPassword, out var br)) return br;
        if (accountName.IsBlank()) return BadRequest();
        if (model == null) return BadRequest("Missing creation model.");
        if (model.ExternalAccount == null) return BadRequest("Missing external account name.");
        if (model.Broker == BrokerType.Unknown) return BadRequest("Invalid broker.");
        if (model.Environment == EnvironmentType.Unknown) return BadRequest("Invalid environment.");
        if (accountName.Length < 3) return BadRequest("Account name should at least have 3 chars.");

        var user = await adminService.GetUser(model.OwnerName, model.Environment);
        if (user == null) return BadRequest("Invalid owner.");
        var brokerId = ExternalNames.GetBrokerId(model.Broker);

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
    /// WARNING, this will erase all the data. Rebuild all the tables.
    /// </summary>
    /// <returns></returns>
    [HttpPost("rebuild-security-definition-tables")]
    public async Task<ActionResult> RebuildSecurityDefinitionTables([FromServices] IStorage storage, [FromForm(Name = "admin-password")] string password)
    {
        if (password.IsBlank()) return BadRequest();
        if (!Credential.IsAdminPasswordCorrect(password)) return BadRequest();

        await storage.CreateSecurityTable(SecurityType.Equity);
        await storage.CreateSecurityTable(SecurityType.Fx);

        var tuples = new (string table, string db)[]
        {
            (DatabaseNames.StockDefinitionTable, DatabaseNames.StaticData),
            (DatabaseNames.FxDefinitionTable, DatabaseNames.StaticData),
        };
        var results = await Task.WhenAll(tuples.Select(async t =>
        {
            var (table, db) = t;
            var r = await storage.CheckTableExists(table, db);
            return (table, r);
        }));
        return Ok(results.ToDictionary(p => p.table, p => p.r));
    }

    /// <summary>
    /// WARNING, this will erase all the data. Rebuild all the price tables.
    /// </summary>
    /// <param name="storage"></param>
    /// <param name="password">Mandatory</param>
    /// <param name="intervalStr">Must be used along with <paramref name="secTypeStr"/>. Only supports 1m, 1h or 1d. If not set, all will be rebuilt.</param>
    /// <param name="secTypeStr">Must be used along with <paramref name="intervalStr"/>.</param>
    /// <returns></returns>
    [HttpPost("rebuild-price-tables")]
    public async Task<ActionResult> RebuildPriceTables([FromServices] IStorage storage,
                                                       [FromForm(Name = "admin-password")] string password,
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
                await storage.CreatePriceTable(interval, secType);
                var r = await storage.CheckTableExists(table, db);
                return (table, r);
            }));
            return Ok(results.ToDictionary(p => p.table, p => p.r));
        }
        else if (interval is IntervalType.OneMinute or IntervalType.OneHour or IntervalType.OneDay)
        {
            var table = DatabaseNames.GetPriceTableName(interval, secType);
            await storage.CreatePriceTable(interval, secType);
            var r = await storage.CheckTableExists(table, DatabaseNames.MarketData);
            return Ok(new Dictionary<string, bool> { { table, r } });
        }

        return BadRequest($"Invalid parameter combination: {intervalStr}, {secTypeStr}");
    }

    /// <summary>
    /// WARNING, this will erase all the data. Rebuild tables with specific type.
    /// </summary>
    /// <param name="storage"></param>
    /// <param name="password"></param>
    /// <param name="secTypeStr">Only Order, Trade, Position and Price tables support security type.</param>
    /// <param name="tableType">Unknown to create everything; or else Order, Trade, Position, FinancialStat, etc.</param>
    /// <returns></returns>
    [HttpPost("rebuild-tables-except-prices")]
    public async Task<ActionResult> RebuildOtherTables([FromServices] IStorage storage,
                                                       [FromForm(Name = "admin-password")] string password,
                                                       [FromQuery(Name = "table-type")] DataType tableType,
                                                       [FromQuery(Name = "sec-type")] string? secTypeStr = null)
    {
        if (ControllerValidator.IsAdminPasswordBad(password, out var br)) return br;

        SecurityType secType = SecurityType.Unknown;
        if (secTypeStr != null)
            secType = SecurityTypeConverter.Parse(secTypeStr);

        Dictionary<string, bool>? results = null;
        if (tableType != DataType.Unknown)
        {
            results = await CreateTables(storage, tableType, secType);
        }
        else if (tableType == DataType.Unknown && secTypeStr.IsBlank())
        {
            results = new();
            results.AddRange(await CreateTables(storage, DataType.User));
            results.AddRange(await CreateTables(storage, DataType.Account));
            results.AddRange(await CreateTables(storage, DataType.Asset));
            results.AddRange(await CreateTables(storage, DataType.FinancialStat));
            results.AddRange(await CreateTables(storage, DataType.AlgoEntry));
            results.AddRange(await CreateTables(storage, DataType.Order, SecurityType.Fx));
            results.AddRange(await CreateTables(storage, DataType.Order, SecurityType.Equity));
            results.AddRange(await CreateTables(storage, DataType.Trade, SecurityType.Fx));
            results.AddRange(await CreateTables(storage, DataType.Trade, SecurityType.Equity));
            results.AddRange(await CreateTables(storage, DataType.Position, SecurityType.Fx));
            results.AddRange(await CreateTables(storage, DataType.Position, SecurityType.Equity));
        }
        return results.IsNullOrEmpty() ? BadRequest($"Invalid parameters: either {tableType} or {secTypeStr} is wrong.") : Ok(results);
    }

    private async Task<Dictionary<string, bool>?> CreateTables(IStorage storage, DataType dataType, SecurityType secType = SecurityType.Unknown)
    {
        List<string> resultTableNames = new();
        switch (dataType)
        {
            case DataType.FinancialStat:
                await storage.CreateFinancialStatsTable();
                resultTableNames.Add(DatabaseNames.FinancialStatsTable);
                break;
            case DataType.Account:
                await storage.CreateAccountTable();
                resultTableNames.Add(DatabaseNames.AccountTable);
                break;
            case DataType.Asset:
                await storage.CreateAssetTable();
                resultTableNames.Add(DatabaseNames.AssetTable);
                break;
            case DataType.User:
                await storage.CreateUserTable();
                resultTableNames.Add(DatabaseNames.UserTable);
                break;
            case DataType.AlgoEntry:
                var (table, database) = await storage.CreateTable<AlgoEntry>();
                resultTableNames.Add(table);
                break;
        }

        if (secType is SecurityType.Equity or SecurityType.Fx)
        {
            switch (dataType)
            {
                case DataType.Order:
                    resultTableNames.AddRange(await storage.CreateOrderTable(secType));
                    break;
                case DataType.Trade:
                    resultTableNames.AddRange(await storage.CreateTradeTable(secType));
                    break;
                case DataType.Position:
                    resultTableNames.AddRange(await storage.CreatePositionTable(secType));
                    break;
                    //case DataType.TradeOrderPositionRelationship:
                    //    results = new List<string> { await storage.CreateTradeOrderPositionIdTable(secType) };
                    //    break;
            }
        }

        var results = new Dictionary<string, bool>();
        foreach (var tn in resultTableNames)
        {
            var r = await storage.CheckTableExists(tn, DatabaseNames.ExecutionData);
            if (!r)
                r = await storage.CheckTableExists(tn, DatabaseNames.StaticData);
            if (!r)
                r = await storage.CheckTableExists(tn, DatabaseNames.MarketData);
            results[tn] = r;
        }
        return results;
    }


    public class UserCreationModel
    {
        [FromForm(Name = "adminPassword")]
        public string? AdminPassword { get; set; }

        [FromForm(Name = "userPassword")]
        public string? UserPassword { get; set; }

        [FromForm(Name = "email")]
        public string? Email { get; set; }

        [FromForm(Name = "environment")]
        public EnvironmentType Environment { get; set; }
    }


    public class AccountCreationModel
    {
        [FromForm(Name = "adminPassword")]
        public string? AdminPassword { get; set; }

        [FromForm(Name = "ownerName")]
        public string? OwnerName { get; set; }

        [FromForm(Name = "brokerType")]
        public BrokerType Broker { get; set; }

        [FromForm(Name = "externalAccount")]
        public string? ExternalAccount { get; set; }

        [FromForm(Name = "type")]
        public string? Type { get; set; }

        [FromForm(Name = "subType")]
        public string? SubType { get; set; }

        [FromForm(Name = "feeStructure")]
        public string? FeeStructure { get; set; }

        [FromForm(Name = "environment")]
        public EnvironmentType Environment { get; set; }
    }
}