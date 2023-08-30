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
    /// <param name="accountName"></param>
    /// <param name="environment"></param>
    /// <returns></returns>
    Task<bool> Login(User user, string password, string accountName, EnvironmentType environment);
}
