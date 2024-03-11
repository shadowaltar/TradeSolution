using Common;
using log4net;
using System.Diagnostics;
using System.Text.Json.Nodes;
using TradeCommon.Essentials.Accounts;
using TradeCommon.Externals;
using TradeCommon.Runtime;
using TradeCommon.Utils;
using TradeConnectivity.CryptoSimulator.Utils;

namespace TradeConnectivity.CryptoSimulator.Services;
public class AccountManager : IExternalAccountManagement
{
    private static readonly ILog _log = Logger.New();
    private static readonly List<string> _accountTypes = ["SIM"];
    private readonly IExternalConnectivityManagement _connectivity;
    private readonly HttpClient _httpClient;
    private readonly KeyManager _keyManager;
    private readonly RequestBuilder _requestBuilder;

    public AccountManager(IExternalConnectivityManagement connectivity,
                          ApplicationContext context,
                          KeyManager keyManager)
    {
        _httpClient = new FakeHttpClient();
        _connectivity = connectivity;
        _keyManager = keyManager;
        _requestBuilder = new RequestBuilder(keyManager, Constants.ReceiveWindowMsString);
    }

    public ResultCode Login(User user, Account account)
    {
        var getSecretResult = _keyManager.Use(user, account);
        if (getSecretResult != ResultCode.GetSecretOk)
            _log.Error("Failed to get secret. ResultCode: " + getSecretResult);
        return getSecretResult == ResultCode.GetSecretOk ? ResultCode.LoginUserAndAccountOk : getSecretResult;
    }

    public void Logout(User user, Account account)
    {
        // binance do nothing
    }

    /// <summary>
    /// Get the account information [SIGNED].
    /// If a list of asset is provided, the assets will be created too.
    /// </summary>
    /// <returns></returns>
    public async Task<ExternalQueryState> GetAccount()
    {
        var account = new Account
        {
            Type = _accountTypes[0],
            ExternalAccount = Guid.NewGuid().ToString(),
            UpdateTime = DateTime.UtcNow,
        };
        return ExternalQueryStates.Simulation<Account>(account).RecordTimes(-1, -1);
    }

    /// <summary>
    /// TODO (Not Tested) Get the account detailed info [SIGNED] [PROD ONLY].
    /// </summary>
    /// <returns></returns>
    public async Task<ExternalQueryState> GetAccount(string accountType)
    {
        return await GetAccount();
    }
}
