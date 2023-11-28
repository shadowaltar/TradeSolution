using Autofac;
using Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using TradeCommon.Constants;
using TradeCommon.Database;
using TradeCommon.Essentials;
using TradeCommon.Essentials.Accounts;
using TradeCommon.Essentials.Algorithms;
using TradeCommon.Essentials.Instruments;
using TradeCommon.Runtime;
using TradeDataCore.StaticData;
using TradeLogicCore.Services;
using TradePort.Controllers.Models;
using TradePort.Utils;

namespace TradePort.Controllers;

/// <summary>
/// Provides admin tasks access.
/// </summary>
[ApiController]
[Route(RestApiConstants.AdminRoot)]
public partial class AdminController : Controller
{
    private JwtSecurityTokenHandler? _tokenHandler;

    /// <summary>
    /// Login with a user and account to an environment and exchange.
    /// </summary>
    /// <param name="context"></param>
    /// <param name="container"></param>
    /// <param name="adminService"></param>
    /// <param name="model"></param>
    /// <returns></returns>
    [AllowAnonymous]
    [HttpPost(RestApiConstants.Login)]
    public async Task<ActionResult> Login([FromServices] Context context,
                                          [FromServices] IComponentContext container,
                                          [FromServices] IAdminService adminService,
                                          [FromForm] LoginRequestModel model)
    {
        if (ControllerValidator.IsAdminPasswordBad(model.AdminPassword, context.Environment, out var br)) return br;

        if (adminService.IsLoggedIn)
        {
            if (!adminService.IsLoggedInWith(model.UserName, model.AccountName))
                return BadRequest("Please logout first.");

            return Ok(CreateLoginResponseModel(ResultCode.AlreadyLoggedIn, context, HttpContext.Session));
        }

        if (!HttpContext.IsSessionAvailable())
            throw Exceptions.Impossible("Erroneous session configuration!");

        if (!HttpContext.IsAuthenticationAvailable())
            throw Exceptions.Impossible("Erroneous authentication configuration!");

        // not initialized here but during web app start up
        // context.Initialize(model.Environment, model.Exchange, broker);

        ResultCode result = await adminService.Login(model.UserName, model.Password, model.AccountName, adminService.Context.Environment);
        if (result != ResultCode.LoginUserAndAccountOk)
            return BadRequest($"Failed to {nameof(Login)}; reason: {result}");

        if (context.User == null || context.Account == null)
            throw Exceptions.Impossible();

        return Ok(CreateLoginResponseModel(result, context, HttpContext.Session));
    }

    /// <summary>
    /// Logout. It will affect all the other sessions (different apps or browsers).
    /// Cannot be executed when there are running algorithms.
    /// </summary>
    /// <param name="context"></param>
    /// <param name="adminService"></param>
    /// <param name="adminPassword"></param>
    /// <returns></returns>
    [AllowAnonymous]
    [HttpPost(RestApiConstants.Logout)]
    public async Task<ActionResult> Logout([FromServices] Context context,
                                           [FromServices] IAdminService adminService,
                                           [FromForm(Name = "admin-password")] string adminPassword)
    {
        if (ControllerValidator.IsAdminPasswordBad(adminPassword, context.Environment, out ObjectResult? br)) return br;

        return !adminService.IsLoggedIn ? BadRequest("Cannot log out if not logged in.") : Ok(await adminService.Logout());
    }

    /// <summary>
    /// Change a user's password.
    /// </summary>
    /// <param name="context"></param>
    /// <param name="adminService"></param>
    /// <param name="model"></param>
    /// <returns></returns>
    [HttpPost(RestApiConstants.ChangeUserPassword)]
    public async Task<ActionResult> ChangeUserPassword([FromServices] Context context,
                                                       [FromServices] IAdminService adminService,
                                                       [FromForm] ChangeUserPasswordModel model)
    {
        if (ControllerValidator.IsAdminPasswordBad(model.AdminPassword, context.Environment, out ObjectResult? br)) return br;
        if (model.NewPassword.IsBlank() || model.NewPassword.Length < Consts.PasswordMinLength) return BadRequest("Password should at least have 6 chars.");

        int r = await adminService.SetPassword(model.UserName, model.NewPassword, model.Environment);
        return r > 0 ? Ok("Password is set.") : BadRequest("Failed to set password.");
    }

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

        User? user = await adminService.GetUser(userName, adminService.Context.Environment);
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
        Account? account = await adminService.GetAccount(accountName, requestExternal);
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
        Account? account = await adminService.GetAccount(accountName);
        if (account == null) return BadRequest("Invalid account name.");

        Account? external = await adminService.GetAccount(accountName, true);

        return Ok(account);
    }

    /// <summary>
    /// Create a new user.
    /// </summary>
    /// <param name="context"></param>
    /// <param name="adminService"></param>
    /// <param name="model"></param>
    /// <param name="userName"></param>
    /// <returns></returns>
    [HttpPost("users/{user}")]
    public async Task<ActionResult> CreateUser([FromServices] Context context,
                                               [FromServices] IAdminService adminService,
                                               [FromForm] UserCreationModel model,
                                               [FromRoute(Name = "user")] string userName)
    {
        if (userName.IsBlank()) return BadRequest();
        if (model == null) return BadRequest();
        if (model.UserPassword.IsBlank()) return BadRequest();
        if (model.AdminPassword.IsBlank()) return BadRequest();
        if (model.Email.IsBlank() || !model.Email.IsValidEmail()) return BadRequest();
        if (!Credential.IsAdminPasswordCorrect(model.AdminPassword, context.Environment)) return BadRequest();

        if (userName.Length < 3) return BadRequest("User name should at least have 3 chars.");
        if (model.UserPassword.Length < Consts.PasswordMinLength) return BadRequest("Password should at least have 6 chars.");

        User? user = await adminService.GetUser(userName, model.Environment);
        if (user != null)
        {
            return BadRequest();
        }

        int result = await adminService.CreateUser(userName, model.UserPassword, model.Email, model.Environment);
        model.UserPassword = "";

        return Ok(result);
    }

    /// <summary>
    /// Create a new account.
    /// </summary>
    /// <param name="context"></param>
    /// <param name="adminService"></param>
    /// <param name="model"></param>
    /// <param name="accountName"></param>
    /// <returns></returns>
    [HttpPost("accounts/{account}")]
    public async Task<ActionResult> CreateAccount([FromServices] Context context,
                                                  [FromServices] IAdminService adminService,
                                                  [FromForm] AccountCreationModel model,
                                                  [FromRoute(Name = "account")] string accountName = "test")
    {
        if (ControllerValidator.IsAdminPasswordBad(model.AdminPassword, context.Environment, out ObjectResult? br)) return br;
        if (accountName.IsBlank()) return BadRequest();
        if (model == null) return BadRequest("Missing creation model.");
        if (model.ExternalAccount == null) return BadRequest("Missing external account name.");
        if (model.Broker == BrokerType.Unknown) return BadRequest("Invalid broker.");
        if (accountName.Length < 3) return BadRequest("Account name should at least have 3 chars.");

        User? user = await adminService.GetUser(model.OwnerName, context.Environment);
        if (user == null) return BadRequest("Invalid owner.");
        int brokerId = ExternalNames.GetBrokerId(model.Broker);

        DateTime now = DateTime.UtcNow;
        var account = new Account
        {
            OwnerId = user.Id,
            Name = accountName,
            Type = model.Type,
            SubType = model.SubType,
            BrokerId = brokerId,
            CreateTime = now,
            UpdateTime = now,
            ExternalAccount = model.ExternalAccount,
            FeeStructure = model.FeeStructure,
        };
        int result = await adminService.CreateAccount(account);
        return Ok(result);
    }

    /// <summary>
    /// WARNING, this will erase all the data. Rebuild all the tables.
    /// </summary>
    /// <returns></returns>
    [HttpPost("rebuild-security-definition-tables")]
    public async Task<ActionResult> RebuildSecurityDefinitionTables([FromServices] Context context,
                                                                    [FromServices] IStorage storage,
                                                                    [FromForm(Name = "admin-password")] string password)
    {
        if (password.IsBlank()) return BadRequest();
        if (!Credential.IsAdminPasswordCorrect(password, context.Environment)) return BadRequest();

        await storage.CreateSecurityTable(SecurityType.Equity);
        await storage.CreateSecurityTable(SecurityType.Fx);

        var tuples = new (string table, string db)[]
        {
            (DatabaseNames.StockDefinitionTable, DatabaseNames.StaticData),
            (DatabaseNames.FxDefinitionTable, DatabaseNames.StaticData),
        };
        (string table, bool r)[] results = await Task.WhenAll(tuples.Select(async t =>
        {
            (string table, string db) = t;
            bool r = await storage.CheckTableExists(table, db);
            return (table, r);
        }));
        return Ok(results.ToDictionary(p => p.table, p => p.r));
    }

    /// <summary>
    /// WARNING, this will erase all the data. Rebuild all the price tables.
    /// </summary>
    /// <param name="context"></param>
    /// <param name="storage"></param>
    /// <param name="password">Mandatory</param>
    /// <param name="intervalStr">Must be used along with <paramref name="secTypeStr"/>. Only supports 1m, 1h or 1d. If not set, all will be rebuilt.</param>
    /// <param name="secTypeStr">Must be used along with <paramref name="intervalStr"/>.</param>
    /// <returns></returns>
    [HttpPost("rebuild-price-tables")]
    public async Task<ActionResult> RebuildPriceTables([FromServices] Context context,
                                                       [FromServices] IStorage storage,
                                                       [FromForm(Name = "admin-password")] string password,
                                                       [FromQuery(Name = "interval")] string? intervalStr,
                                                       [FromQuery(Name = "sec-type")] string? secTypeStr)
    {
        if (password.IsBlank()) return BadRequest();
        if (!Credential.IsAdminPasswordCorrect(password, context.Environment)) return BadRequest();

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
            (string table, bool r)[] results = await Task.WhenAll(tuples.Select(async t =>
            {
                (string table, string db, IntervalType interval, SecurityType secType) = t;
                await storage.CreatePriceTable(interval, secType);
                bool r = await storage.CheckTableExists(table, db);
                return (table, r);
            }));
            return Ok(results.ToDictionary(p => p.table, p => p.r));
        }
        else if (interval is IntervalType.OneMinute or IntervalType.OneHour or IntervalType.OneDay)
        {
            string table = DatabaseNames.GetPriceTableName(interval, secType);
            await storage.CreatePriceTable(interval, secType);
            bool r = await storage.CheckTableExists(table, DatabaseNames.MarketData);
            return Ok(new Dictionary<string, bool> { { table, r } });
        }

        return BadRequest($"Invalid parameter combination: {intervalStr}, {secTypeStr}");
    }

    /// <summary>
    /// WARNING, this will erase all the data. Rebuild tables with specific type.
    /// </summary>
    /// <param name="context"></param>
    /// <param name="storage"></param>
    /// <param name="password"></param>
    /// <param name="secTypeStr">Only Order, Trade, Position and Price tables support security type.</param>
    /// <param name="tableType">Unknown to create everything; or else Order, Trade, Position, FinancialStat, etc.</param>
    /// <returns></returns>
    [HttpPost("rebuild-tables-except-prices")]
    public async Task<ActionResult> RebuildOtherTables([FromServices] Context context,
                                                       [FromServices] IStorage storage,
                                                       [FromForm(Name = "admin-password")] string password,
                                                       [FromQuery(Name = "table-type")] DataType tableType,
                                                       [FromQuery(Name = "sec-type")] string? secTypeStr = null)
    {
        if (ControllerValidator.IsAdminPasswordBad(password, context.Environment, out ObjectResult? br)) return br;

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
    [AllowAnonymous]
    [HttpGet("ping")]
    public async Task<ActionResult> Ping()
    {
        return Ok("{\"pong\":true}");
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
                (string table, string database) = await storage.CreateTable<AlgoEntry>();
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
                    //    results = new ListAlgoBatches<string> { await storage.CreateTradeOrderPositionIdTable(secType) };
                    //    break;
            }
        }

        var results = new Dictionary<string, bool>();
        foreach (string tn in resultTableNames)
        {
            bool r = await storage.CheckTableExists(tn, DatabaseNames.ExecutionData);
            if (!r)
                r = await storage.CheckTableExists(tn, DatabaseNames.StaticData);
            if (!r)
                r = await storage.CheckTableExists(tn, DatabaseNames.MarketData);
            results[tn] = r;
        }
        return results;
    }

    private LoginResponseModel CreateLoginResponseModel(ResultCode result, Context context, ISession session)
    {
        if (context.User == null || context.Account == null)
            throw Exceptions.Impossible();

        _tokenHandler ??= new JwtSecurityTokenHandler();

        session.SetString("UserName", context.User.Name);
        session.SetInt32("UserId", context.User.Id);
        session.SetString("AccountName", context.Account.Name);
        session.SetInt32("AccountId", context.Account.Id);
        var tokenDescriptor = Authentication.GetTokenDescriptor(context, session.Id);
        var token = _tokenHandler.CreateToken(tokenDescriptor);
        var tokenString = _tokenHandler.WriteToken(token);
        return new LoginResponseModel(result, context.User.Id, context.User.Name, context.User.Email, context.Account.Id, context.Account.Name, context.Exchange, context.Broker, context.Environment, token.ValidTo, tokenString);
    }
}