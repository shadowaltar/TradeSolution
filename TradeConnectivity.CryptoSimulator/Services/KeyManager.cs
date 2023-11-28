using Common;
using TradeCommon.Essentials.Accounts;
using TradeCommon.Runtime;

namespace TradeConnectivity.CryptoSimulator.Services;
public class KeyManager
{
    /// <summary>
    /// Sign in current user and account, and use its credentials / keys.
    /// </summary>
    /// <param name="user"></param>
    /// <returns></returns>
    public ResultCode Use(User user, Account account)
    {
        Assertion.ShallNever(user == null);
        return ResultCode.GetSecretOk;
    }
}

