using Autofac;
using Common;
using log4net;
using System.Diagnostics;
using System.Text.Json.Nodes;
using TradeCommon.Essentials.Accounts;
using TradeCommon.Essentials.Instruments;
using TradeCommon.Essentials.Portfolios;
using TradeCommon.Externals;
using TradeCommon.Runtime;
using TradeConnectivity.Binance.Utils;

namespace TradeConnectivity.Binance.Services;
public class AccountManager : IExternalAccountManagement
{
    private static readonly ILog _log = Logger.New();
    private static readonly List<string> _accountTypes = new() { "SPOT", "MARGIN", "FUTURES" };
    private readonly IExternalConnectivityManagement _connectivity;
    private readonly HttpClient _httpClient;
    private readonly KeyManager _keyManager;
    private readonly RequestBuilder _requestBuilder;

    public AccountManager(IExternalConnectivityManagement connectivity,
                          HttpClient httpClient,
                          ApplicationContext context,
                          KeyManager keyManager)
    {
        if (context.IsExternalProhibited)
            _httpClient = new FakeHttpClient();
        else
            _httpClient = httpClient;
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

    /// <summary>
    /// Get the account information [SIGNED].
    /// If a list of asset is provided, the assets will be created too.
    /// </summary>
    /// <returns></returns>
    public async Task<ExternalQueryState> GetAccount()
    {
        var swOuter = Stopwatch.StartNew();
        var url = $"{_connectivity.RootUrl}/api/v3/account";
        using var request = _requestBuilder.BuildSigned(HttpMethod.Get, url);

        var (response, rtt) = await _httpClient.TimedSendAsync(request, log: _log);
        var connId = response.CheckHeaders();
        if (!response.ParseJsonObject(out var content, out var json, out var errorMessage, _log))
            return ExternalQueryStates.Error(ActionType.GetAccount, ResultCode.GetAccountFailed, content, connId, errorMessage);

        // example json: responseJson = @"{ ""makerCommission"": 0, ""takerCommission"": 0, ""buyerCommission"": 0, ""sellerCommission"": 0, ""commissionRates"": { ""maker"": ""0.00000000"", ""taker"": ""0.00000000"", ""buyer"": ""0.00000000"", ""seller"": ""0.00000000"" }, ""canTrade"": true, ""canWithdraw"": false, ""canDeposit"": false, ""brokered"": false, ""requireSelfTradePrevention"": false, ""preventSor"": false, ""updateTime"": 1690995029309, ""accountType"": ""SPOT"", ""assets"": [ { ""asset"": ""BNB"", ""free"": ""1000.00000000"", ""locked"": ""0.00000000"" }, { ""asset"": ""BTC"", ""free"": ""1.00000000"", ""locked"": ""0.00000000"" }, { ""asset"": ""BUSD"", ""free"": ""10000.00000000"", ""locked"": ""0.00000000"" }, { ""asset"": ""ETH"", ""free"": ""100.00000000"", ""locked"": ""0.00000000"" }, { ""asset"": ""LTC"", ""free"": ""500.00000000"", ""locked"": ""0.00000000"" }, { ""asset"": ""TRX"", ""free"": ""500000.00000000"", ""locked"": ""0.00000000"" }, { ""asset"": ""USDT"", ""free"": ""8400.00000000"", ""locked"": ""1600.00000000"" }, { ""asset"": ""XRP"", ""free"": ""50000.00000000"", ""locked"": ""0.00000000"" } ], ""permissions"": [ ""SPOT"" ], ""uid"": 1688996631782681271 }";
        var account = new Account
        {
            Type = json.GetString("accountType"),
            ExternalAccount = json.GetLong("uid").ToString(),
            UpdateTime = json.GetLong("updateTime").FromUnixMs(),
        };
        return ExternalQueryStates.QueryAccount(content, connId, account).RecordTimes(rtt, swOuter);
    }

    /// <summary>
    /// Get the account detailed info [SIGNED] [PROD ONLY].
    /// </summary>
    /// <returns></returns>
    public async Task<ExternalQueryState> GetAccount(string accountType)
    {
        var swTotal = Stopwatch.StartNew();
        var accounts = new List<Account>(_accountTypes.Count);

        var url = $"{_connectivity.RootUrl}/sapi/v1/accountSnapshot";
        var parameters = new List<(string, string)>
        {
            ("type", accountType),
            ("limit", 7.ToString()), // the api returns a history list of account statuses; min is 7
        };
        using var request = _requestBuilder.Build(HttpMethod.Get, url, parameters);

        var (response, rtt) = await _httpClient.TimedSendAsync(request, log: _log);
        var connId = response.CheckHeaders();
        if (!response.ParseJsonObject(out var content, out var rootObj, out var errorMessage, _log))
        {
            return ExternalQueryStates.Error(ActionType.GetAccount, ResultCode.GetAccountFailed, content, connId, errorMessage).RecordTimes(rtt, swTotal);
        }

        var account = Parse(rootObj);
        if (account != null)
        {
            accounts.Add(account);
            return ExternalQueryStates.QueryAccount(content, connId, account).RecordTimes(rtt, swTotal);
        }
        else
        {
            _log.Error($"Failed to get or parse account (type: {accountType}) response.");
            return ExternalQueryStates.Error(ActionType.GetAccount, ResultCode.GetAccountFailed, content, connId, errorMessage);
        }

        Account Parse(JsonObject rootObj)
        {
            throw new NotImplementedException();
        }
    }
}
