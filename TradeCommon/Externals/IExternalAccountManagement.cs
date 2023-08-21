using TradeCommon.Essentials.Accounts;
using TradeCommon.Runtime;

namespace TradeCommon.Externals;
public interface IExternalAccountManagement
{
    bool Login(User user);

    Task<ExternalQueryState<List<Account>>> GetAccounts();

    Task<ExternalQueryState<Account?>> GetAccount();
}
