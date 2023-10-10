using Autofac;
using Autofac.Core;
using Common;
using log4net;
using Microsoft.IdentityModel.Tokens;
using TradeCommon.Constants;
using TradeCommon.Database;
using TradeCommon.Essentials.Accounts;
using TradeCommon.Essentials.Portfolios;
using TradeCommon.Externals;
using TradeCommon.Runtime;
using TradeDataCore.Instruments;
using TradeDataCore.StaticData;

namespace TradeLogicCore.Services;
public class AdminService : IAdminService
{
    private static readonly ILog _log = Logger.New();
    private readonly IComponentContext _container;
    private readonly IStorage _storage;
    private readonly ISecurityService _securityService;
    private readonly ITradeService _tradeService;
    private readonly IPortfolioService _portfolioService;
    private readonly Persistence _persistence;
    private readonly IExternalAccountManagement _accountManagement;
    private readonly IExternalConnectivityManagement _connectivity;

    public User? CurrentUser { get; private set; }

    public Account? CurrentAccount { get; private set; }

    public Context Context { get; }

    public AdminService(IComponentContext container,
                        Context context,
                        Persistence persistence,
                        ISecurityService securityService,
                        IPortfolioService portfolioService,
                        ITradeService tradeService,
                        IExternalAccountManagement accountManagement,
                        IExternalConnectivityManagement connectivity)
    {
        Context = context;
        _container = container;
        _storage = context.Storage;
        _securityService = securityService;
        _tradeService = tradeService;
        _portfolioService = portfolioService;
        _persistence = persistence;
        _accountManagement = accountManagement;
        _connectivity = connectivity;
    }

    public void Initialize(EnvironmentType environment, ExchangeType exchange, BrokerType broker)
    {
        _log.Info($"Initializing admin-service: {environment}, {exchange}, {broker}");
        Context.Initialize(_container, environment, exchange, broker);
        _connectivity.SetEnvironment(environment);

        _securityService.Initialize();
        _tradeService.Initialize();
    }

    public async Task<ResultCode> Login(string userName, string? password, string? accountName, EnvironmentType environment)
    {
        _log.Info($"Logging in user and account: {userName}, {accountName}, {environment}");
        if (userName.IsBlank()) return ResultCode.InvalidArgument;
        if (password.IsBlank()) return ResultCode.InvalidArgument;
        if (accountName.IsBlank()) return ResultCode.InvalidArgument;

        CurrentUser = null;
        CurrentAccount = null;

        var user = await GetUser(userName, Context.Environment);
        if (user == null) return ResultCode.GetUserFailed;
        CurrentUser = user;

        Assertion.Shall(Enum.Parse<EnvironmentType>(user.Environment, true) == environment);
        if (!Credential.IsPasswordCorrect(user, password))
        {
            _log.Error($"Failed to login user {user.Name} in env {user.Environment}.");
            return ResultCode.InvalidCredential;
        }

        var account = await GetAccount(accountName, environment);
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
        _log.Info($"Logged in user {user.Name} in env {user.Environment} with account {accountName}");

        user.Accounts.Add(account);
        CurrentAccount = account;

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
        return ResultCode.LoginUserAndAccountOk;
    }

    public async Task<int> CreateUser(string userName, string userPassword, string email, EnvironmentType environment)
    {
        if (userName.IsBlank() || userPassword.IsBlank() || email.IsBlank())
        {
            return -1;
        }
        userName = userName.Trim().ToLowerInvariant();
        userPassword = userPassword.Trim().ToLowerInvariant();
        email = email.Trim().ToLowerInvariant();
        var now = DateTime.UtcNow;
        var user = new User
        {
            Name = userName,
            Email = email,
            CreateTime = now,
            UpdateTime = now,
            Environment = Environments.ToString(environment),
        };
        Credential.EncryptUserPassword(user, ref userPassword);

        user.ValidateOrThrow();
        user.AutoCorrect();
        return await _storage.InsertOne(user, false);
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
        if (account.Id <= 0) throw new ArgumentException("Invalid account type.");
        if (account.Type.IsBlank()) throw new ArgumentException("Invalid account type.");

        account.ValidateOrThrow();
        account.AutoCorrect();
        return await _storage.InsertOne(account, false);
    }

    public async Task<Account?> GetAccount(string? accountName, EnvironmentType environment, bool requestExternal = false)
    {
        if (CurrentUser == null) throw new InvalidOperationException("Must get user before get account.");

        if (accountName.IsBlank()) return null;

        var assets = _securityService.GetAssets(Context.Exchange);

        if (!requestExternal)
        {
            var account = await _storage.ReadAccount(accountName, environment);
            if (account == null)
            {
                _log.Error($"Failed to read account by name {accountName} from database in {environment}.");
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
                if (account.Environment == EnvironmentType.Unknown)
                    account.Environment = Context.Environment;
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
