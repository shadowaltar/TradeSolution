using TradeCommon.Essentials.Accounts;
using TradeCommon.Runtime;

namespace TradeCommon.Externals;
public interface IExternalAccountManagement
{
    bool Login(User user);

    Task<ExternalQueryState> GetAccounts();

    Task<ExternalQueryState> GetAccount();
}
