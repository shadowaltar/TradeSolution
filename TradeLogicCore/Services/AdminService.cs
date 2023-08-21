using Common;
using log4net;
using System.Runtime.InteropServices;
using TradeCommon.Constants;
using TradeCommon.Database;
using TradeCommon.Essentials.Accounts;
using TradeCommon.Externals;
using TradeCommon.Runtime;
using TradeDataCore.StaticData;

namespace TradeLogicCore.Services;
public class AdminService : IAdminService
{
    private static readonly ILog _log = Logger.New();
    private readonly IExternalAccountManagement _accountManagement;

    public AdminService(IExternalAccountManagement accountManagement)
    {
        _accountManagement = accountManagement;
    }

    public bool Login(User user, string password, EnvironmentType environment)
    {
        Assertion.Shall(Enum.Parse<EnvironmentType>(user.Environment, true) == environment);
        if (Credential.IsPasswordCorrect(user, password) && _accountManagement.Login(user))
        {
            _log.Error($"Failed to login user {user.Name} in env {user.Environment}.");
            return true;
        }
        _log.Error($"Failed to login user {user.Name} in env {user.Environment}.");
        return false;
    }

    public bool SelectUser(User user)
    {
        return AsyncHelper.RunSync(() => ExternalExecution.Initialize(user));
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

    public async Task<User?> ReadUser(string userName, EnvironmentType environment)
    {
        return await Storage.ReadUser(userName, "", environment);
    }

    public async Task<User?> ReadUserByEmail(string email, EnvironmentType environment)
    {
        return await Storage.ReadUser("", email, environment);
    }

    public async Task<int> CreateAccount(Account account)
    {
        return await Storage.InsertAccount(account);
    }

    public async Task<Account?> ReadAccount(string accountName, EnvironmentType environment)
    {
        return await Storage.ReadAccount(accountName, environment);
    }
}
