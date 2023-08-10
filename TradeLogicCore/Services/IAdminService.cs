using TradeCommon.Essentials.Accounts;

namespace TradeLogicCore.Services;
public interface IAdminService
{
    Task<int> CreateUser(string userName, string userPassword, string email);

    Task<User?> ReadUser(string userName);
    
    Task<User?> ReadUserByEmail(string email);
}
