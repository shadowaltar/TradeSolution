using TradeCommon.Essentials.Accounts;
using TradeCommon.Runtime;

namespace TradeCommon.Externals;
public interface IExternalAccountManagement
{
    ResultCode Login(User user, Account account);

    void Logout(User user, Account account);

    Task<ExternalQueryState> GetAccount(string accountType);

    Task<ExternalQueryState> GetAccount();
}
