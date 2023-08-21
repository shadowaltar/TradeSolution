using TradeCommon.Essentials.Accounts;
using TradeCommon.Runtime;

namespace TradeLogicCore.Services;
public interface IAdminService
{
    Task<int> CreateAccount(Account account);

    Task<Account?> ReadAccount(string accountName, EnvironmentType environment);

    Task<int> CreateUser(string userName, string userPassword, string email, EnvironmentType environment);

    Task<User?> ReadUser(string userName, EnvironmentType environment);

    Task<User?> ReadUserByEmail(string email, EnvironmentType environment);

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
