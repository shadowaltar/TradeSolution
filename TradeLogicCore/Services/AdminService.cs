﻿using Common;
using log4net;
using TradeCommon.Constants;
using TradeCommon.Database;
using TradeCommon.Essentials.Accounts;
using TradeCommon.Essentials.Instruments;
using TradeCommon.Essentials.Portfolios;
using TradeCommon.Externals;
using TradeCommon.Runtime;
using TradeDataCore.Instruments;
using TradeDataCore.StaticData;

namespace TradeLogicCore.Services;
public class AdminService : IAdminService
{
    private static readonly ILog _log = Logger.New();
    private readonly IExternalAccountManagement _accountManagement;
    private readonly IExternalConnectivityManagement _connectivity;
    private readonly ISecurityService _securityService;
    private readonly Persistence _persistence;

    public User? CurrentUser { get; private set; }

    public Account? CurrentAccount { get; private set; }

    public Context Context { get; }

    public AdminService(Context context,
                        IExternalAccountManagement accountManagement,
                        IExternalConnectivityManagement connectivity,
                        ISecurityService securityService,
                        Persistence persistence)
    {
        Context = context;
        _accountManagement = accountManagement;
        _connectivity = connectivity;
        _securityService = securityService;
        _persistence = persistence;
    }

    public void Initialize(EnvironmentType environment, ExchangeType exchange, BrokerType broker)
    {
        Context.Setup(environment, exchange, broker);
        _connectivity.SetEnvironment(environment);
    }

    public async Task<ResultCode> Login(string userName, string? password, string? accountName, EnvironmentType environment)
    {
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
            return ResultCode.LoginUserAndAccountFailed;
        }
        _log.Info($"Logged in user {user.Name} in env {user.Environment} with account {accountName}");

        user.Accounts.Add(account);
        CurrentAccount = account;

        Context.User = CurrentUser;
        Context.Account = CurrentAccount;

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

        return await Storage.InsertUser(user);
    }

    public async Task<User?> GetUser(string? userName, EnvironmentType environment)
    {
        if (userName.IsBlank()) return null;
        return await Storage.ReadUser(userName, "", environment);
    }

    public async Task<User?> GetUserByEmail(string? email, EnvironmentType environment)
    {
        if (email.IsBlank()) return null;
        return await Storage.ReadUser("", email, environment);
    }

    public async Task<int> CreateAccount(Account account)
    {
        return await Storage.InsertAccount(account, false);
    }

    public async Task<Account?> GetAccount(string? accountName, EnvironmentType environment, bool requestExternal = false)
    {
        if (CurrentUser == null) throw new InvalidOperationException("Must get user before get account.");

        if (accountName.IsBlank()) return null;

        var assets = _securityService.GetAssets(Context.Exchange);

        if (!requestExternal)
        {
            var account = await Storage.ReadAccount(accountName, environment);
            if (account == null)
            {
                _log.Error($"Failed to read account by name {accountName} from database.");
                return null;
            }
            var balances = await Storage.ReadBalances(account.Id);
            if (balances.IsNullOrEmpty())
            {
                _log.Warn($"Failed to read balances or no balance records related to account name {accountName} from database.");
            }
            else
            {
                foreach (var balance in balances)
                {
                    balance.AssetCode = assets.FirstOrDefault(a => a.Id == balance.AssetId)?.Code ?? "";
                }
                account.Balances.AddRange(balances);
            }
            return account;
        }
        else
        {
            var state = await _accountManagement.GetAccount(assets);
            var account = state.ContentAs<Account>();
            if (account == null)
                return null;

            // when the storged & broker's external account names are the same
            // then must sync some info which are not on broker side
            if (account.ExternalAccount == CurrentAccount?.ExternalAccount)
            {
                account.Id = CurrentAccount.Id;

                if (!account.CreateTime.IsValid())
                    account.CreateTime = account.UpdateTime;
                if (!account.OwnerId.IsValid())
                    account.OwnerId = CurrentUser.Id;
                if (account.Name.IsBlank())
                    account.Name = CurrentAccount.Name;
                if (account.Environment == EnvironmentType.Unknown)
                    account.Environment = Context.Environment;
                if (!account.BrokerId.IsValid())
                    account.BrokerId = Context.BrokerId;

                foreach (var balance in account.Balances)
                {
                    balance.AccountId = account.Id;
                    balance.UpdateTime = account.UpdateTime;
                }
            }

            //_persistence.Enqueue(new PersistenceTask<Account>(account));
            //if (!account.Balances.IsNullOrEmpty())
            //{
            //    foreach (var balance in account.Balances)
            //    {
            //        _persistence.Enqueue(new PersistenceTask<Balance>(balance));
            //    }
            //}
            return account;
        }
    }
}
