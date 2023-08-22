using TradeCommon.Essentials.Accounts;
using TradeCommon.Runtime;

namespace TradeLogicCore.Services;
public interface IAdminService
{
    Task<int> CreateAccount(Account account);

    Task<Account?> GetAccount(string accountName, EnvironmentType environment, bool requestExternal = false);

    Task<int> CreateUser(string userName, string userPassword, string email, EnvironmentType environment);

    Task<User?> GetUser(string userName, EnvironmentType environment);

    Task<User?> GetUserByEmail(string email, EnvironmentType environment);

    /// <summary>
    /// Login a user.
    /// </summary>
    /// <param name="user"></param>
    /// <param name="password"></param>
    /// <param name="environment"></param>
    /// <returns></returns>
    bool Login(User user, string password, EnvironmentType environment);

    /// <summary>
    /// Select a user in order to use its account / balance / credentials.
    /// </summary>
    /// <param name="user"></param>
    /// <returns></returns>
    bool SelectUser(User user);
}
