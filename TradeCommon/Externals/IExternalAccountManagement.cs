using TradeCommon.Essentials;
using TradeCommon.Runtime;

namespace TradeCommon.Externals;
public interface IExternalAccountManagement
{
    Task<ExternalQueryState<List<Account>>> GetAccounts();

    Task<ExternalQueryState<Account?>> GetAccount();
}
