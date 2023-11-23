using TradeCommon.Constants;
using TradeCommon.Essentials.Accounts;
using TradeCommon.Runtime;

namespace TradeLogicCore.Services;
public interface IAdminService
{
    Context Context { get; }

    /// <summary>
    /// Gets if is logged in.
    /// </summary>
    bool IsLoggedIn { get; }

    User? CurrentUser { get; }

    Account? CurrentAccount { get; }

    /// <summary>
    /// Login a user.
    /// </summary>
    /// <param name="userName"></param>
    /// <param name="password"></param>
    /// <param name="accountName"></param>
    /// <param name="environment"></param>
    /// <returns></returns>
    Task<ResultCode> Login(string userName, string? password, string? accountName, EnvironmentType environment);

    Task<ResultCode> Logout();

    bool IsLoggedInWith(string userName, string accountName, EnvironmentType environment, ExchangeType exchange);

    /// <summary>
    /// Ping external service.
    /// </summary>
    /// <returns></returns>
    Task<bool> Ping();

    Task<Account?> GetAccount(string? accountName, EnvironmentType environment, bool requestExternal = false);

    Task<User?> GetUser(string? userName, EnvironmentType environment);

    Task<User?> GetUserByEmail(string? email, EnvironmentType environment);

    Task<int> CreateAccount(Account account);

    Task<int> CreateUser(string userName, string userPassword, string email, EnvironmentType environment);

    Task<int> SetPassword(string userName, string userPassword, EnvironmentType environment);
}
