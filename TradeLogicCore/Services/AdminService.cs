using Common;
using log4net;
using System;
using TradeCommon.Constants;
using TradeCommon.Database;
using TradeCommon.Essentials.Accounts;
using TradeCommon.Externals;
using TradeCommon.Runtime;
using TradeDataCore.Instruments;
using TradeDataCore.MarketData;
using TradeDataCore.StaticData;

namespace TradeLogicCore.Services;
public class AdminService : IAdminService
{
    private static readonly ILog _log = Logger.New();

    private readonly IStorage _storage;
    private readonly ISecurityService _securityService;
    private readonly ITradeService _tradeService;
    private readonly IPortfolioService _portfolioService;
    private readonly IMarketDataService _marketDataService;
    private readonly IExternalAccountManagement _accountManagement;
    private readonly IExternalConnectivityManagement _connectivity;

    public bool IsLoggedIn { get; private set; }

    public User? CurrentUser { get; private set; }

    public Account? CurrentAccount { get; private set; }

    public Context Context { get; }

    public AdminService(Context context,
                        ISecurityService securityService,
                        IPortfolioService portfolioService,
                        IMarketDataService marketDataService,
                        ITradeService tradeService,
                        IExternalAccountManagement accountManagement,
                        IExternalConnectivityManagement connectivity)
    {
        Context = context;
        _storage = context.Storage;
        _securityService = securityService;
        _tradeService = tradeService;
        _portfolioService = portfolioService;
        _marketDataService = marketDataService;
        _accountManagement = accountManagement;
        _connectivity = connectivity;
    }

    public async Task<ResultCode> Logout()
    {
        if (!IsLoggedIn || Context.User == null || Context.Account == null)
            return ResultCode.NotLoggedInYet;
        if (Context.Core.GetActiveAlgoBatches().Count > 0)
            return ResultCode.ActiveAlgoBatchesExist;

        _accountManagement.Logout(Context.User!, Context.Account!);
        // reset everything
        await Context.Services.Reset();

        _connectivity.SetEnvironment(EnvironmentType.Unknown);

        Context.Reset();
        IsLoggedIn = false;
        return ResultCode.Ok;
    }

    public async Task<ResultCode> Login(string userName, string? password, string? accountName, EnvironmentType environment)
    {
        if (IsLoggedIn)
        {
            return ResultCode.AlreadyLoggedIn;
        }
        IsLoggedIn = false;

        userName = userName.ToUpperInvariant();
        accountName = accountName?.ToUpperInvariant();

        _log.Info($"Logging in user and account: {userName}, {accountName}, {environment}");
        if (userName.IsBlank()) return ResultCode.InvalidArgument;
        if (password.IsBlank()) return ResultCode.InvalidArgument;
        if (accountName.IsBlank()) return ResultCode.InvalidArgument;

        _storage.SetEnvironment(environment);
        _connectivity.SetEnvironment(environment);
        _tradeService.Initialize();

        await _securityService.Initialize();

        CurrentUser = null;
        CurrentAccount = null;

        var user = await GetUser(userName, Context.Environment);
        if (user == null)
            return ResultCode.GetUserFailed;

        if (!Credential.IsPasswordCorrect(user, password, environment))
        {
            _log.Error($"Failed to login user {user.Name} in env {environment}.");
            return ResultCode.InvalidCredential;
        }

        CurrentUser = user;

        var account = await GetAccount(accountName);
        if (account == null)
        {
            _log.Error($"Account {accountName} does not exist or is not associated with user {user.Name}.");
            return ResultCode.GetAccountFailed;
        }
        if (account.OwnerId != user.Id)
        {
            _log.Error($"Account {accountName} is not owned by {user.Name}.");
            return ResultCode.AccountNotOwnedByUser;
        }
        var externalLoginResult = _accountManagement.Login(user, account);
        if (externalLoginResult != ResultCode.LoginUserAndAccountOk)
        {
            _log.Error($"Failed to login account {accountName} with user {user.Name}.");
            return externalLoginResult;
        }
        _log.Info($"Logged in user {user.Name} in env {environment} with account {accountName}");

        user.Accounts.Add(account);
        CurrentAccount = account;
        CurrentUser.LoginSessionId = Guid.NewGuid().ToString();
        Context.User = CurrentUser;
        Context.Account = CurrentAccount;

        var isExternalAvailable = await Ping();
        if (!isExternalAvailable)
        {
            return ResultCode.ConnectionFailed;
        }

        // setup portfolio
        var result = await _portfolioService.Initialize();
        if (!result)
            return ResultCode.SubscriptionFailed;

        await _marketDataService.Initialize();

        IsLoggedIn = true;
        return ResultCode.LoginUserAndAccountOk;
    }

    public bool IsLoggedInWith(string userName, string accountName)
    {
        return CurrentUser?.Name == userName && CurrentAccount?.Name == accountName;
    }

    public async Task<int> CreateUser(string userName, string userPassword, string email, EnvironmentType environment)
    {
        if (userName.IsBlank() || userPassword.IsBlank() || email.IsBlank())
        {
            return -1;
        }
        
        userName = userName.Trim().ToUpperInvariant();

        userPassword = userPassword.Trim().ToLowerInvariant();
        email = email.Trim().ToLowerInvariant();
        var now = DateTime.UtcNow;
        var user = new User
        {
            Name = userName,
            Email = email,
            CreateTime = now,
            UpdateTime = now,
        };
        Credential.EncryptUserPassword(user, environment, ref userPassword);

        user.ValidateOrThrow();
        user.AutoCorrect();
        return await _storage.InsertOne(user);
    }

    public async Task<int> SetPassword(string userName, string userPassword, EnvironmentType environment)
    {
        if (userName.IsBlank() || userPassword.IsBlank())
        {
            return -1;
        }
        var user = await GetUser(userName, environment);
        if (user == null)
        {
            return -1;
        }
        var now = DateTime.UtcNow;
        user = user with
        {
            UpdateTime = now,
        };
        Credential.EncryptUserPassword(user, environment, ref userPassword);
        return await _storage.UpsertOne(user);
    }

    public async Task<User?> GetUser(string? userName, EnvironmentType environment)
    {
        return userName.IsBlank() ? null : await _storage.ReadUser(userName, "", environment);
    }

    public async Task<User?> GetUserByEmail(string? email, EnvironmentType environment)
    {
        return email.IsBlank() ? null : await _storage.ReadUser("", email, environment);
    }

    public async Task<int> CreateAccount(Account account)
    {
        account.Name = account.Name.ToUpperInvariant();

        if (account.Type.IsBlank()) throw new ArgumentException("Invalid account type.");
        return await _storage.InsertOne(account);
    }

    public async Task<Account?> GetAccount(string? accountName, bool requestExternal = false)
    {
        if (CurrentUser == null) throw new InvalidOperationException("Must get user before get account.");

        if (accountName.IsBlank()) return null;

        if (!requestExternal)
        {
            var account = await _storage.ReadAccount(accountName);
            if (account == null)
            {
                _log.Error($"Failed to read account by name {accountName} from database.");
                return null;
            }
            return account;
        }
        else
        {
            var state = await _accountManagement.GetAccount();
            var account = state.Get<Account>();
            if (account == null)
                return null;

            // when the stored & broker's external account names are the same
            // then must sync some info which are not on broker side
            if (account.ExternalAccount == CurrentAccount?.ExternalAccount)
            {
                account.Id = CurrentAccount.Id;

                if (!account.CreateTime.IsValid())
                    account.CreateTime = account.UpdateTime;
                if (account.OwnerId <= 0)
                    account.OwnerId = CurrentUser.Id;
                if (account.Name.IsBlank())
                    account.Name = CurrentAccount.Name;
                if (account.BrokerId <= 0)
                    account.BrokerId = Context.BrokerId;
            }
            return account;
        }
    }

    public async Task<bool> Ping()
    {
        return _connectivity.Ping();
    }
}
