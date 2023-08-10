using TradeCommon.Essentials.Accounts;
using TradeCommon.Runtime;

namespace TradeCommon.Externals;
public interface IExternalAccountManagement
{
    Task<ExternalQueryState<List<Account>>> GetAccounts();

    Task<ExternalQueryState<Account?>> GetAccount();
}
