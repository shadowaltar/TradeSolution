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
}
