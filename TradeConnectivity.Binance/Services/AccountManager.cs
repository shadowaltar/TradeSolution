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

    public AccountManager(IExternalConnectivityManagement connectivity, HttpClient httpClient, KeyManager keyManager)
    {
        _connectivity = connectivity;
        _httpClient = httpClient;
        _keyManager = keyManager;
        _requestBuilder = new RequestBuilder(keyManager, Constants.ReceiveWindowMsString);
    }

    public ResultCode Login(User user, Account account)
    {
        var getSecretResult = _keyManager.Use(user, account);
        if (getSecretResult == ResultCode.GetSecretOk)
            return ResultCode.LoginUserAndAccountOk;

        _log.Error("Failed to get secret. ResultCode: " + getSecretResult);
        return ResultCode.LoginUserAndAccountFailed;
    }

    /// <summary>
    /// Get the account information [SIGNED].
    /// If a list of asset is provided, the balances will be created too.
    /// </summary>
    /// <returns></returns>
    public async Task<ExternalQueryState> GetAccount(List<Security>? assets = null)
    {
        var swOuter = Stopwatch.StartNew();
        var url = $"{_connectivity.RootUrl}/api/v3/account";
        using var request = _requestBuilder.BuildSigned(HttpMethod.Get, url);

        var (response, rtt) = await _httpClient.TimedSendAsync(request);
        var connId = response.CheckHeaders();
        if (!response.ParseJsonObject(out var content, out var json, out var errorMessage, _log))
            return ExternalQueryStates.Error(ActionType.GetAccount, ResultCode.GetAccountFailed, content, connId, errorMessage);

        // example json: responseJson = @"{ ""makerCommission"": 0, ""takerCommission"": 0, ""buyerCommission"": 0, ""sellerCommission"": 0, ""commissionRates"": { ""maker"": ""0.00000000"", ""taker"": ""0.00000000"", ""buyer"": ""0.00000000"", ""seller"": ""0.00000000"" }, ""canTrade"": true, ""canWithdraw"": false, ""canDeposit"": false, ""brokered"": false, ""requireSelfTradePrevention"": false, ""preventSor"": false, ""updateTime"": 1690995029309, ""accountType"": ""SPOT"", ""balances"": [ { ""asset"": ""BNB"", ""free"": ""1000.00000000"", ""locked"": ""0.00000000"" }, { ""asset"": ""BTC"", ""free"": ""1.00000000"", ""locked"": ""0.00000000"" }, { ""asset"": ""BUSD"", ""free"": ""10000.00000000"", ""locked"": ""0.00000000"" }, { ""asset"": ""ETH"", ""free"": ""100.00000000"", ""locked"": ""0.00000000"" }, { ""asset"": ""LTC"", ""free"": ""500.00000000"", ""locked"": ""0.00000000"" }, { ""asset"": ""TRX"", ""free"": ""500000.00000000"", ""locked"": ""0.00000000"" }, { ""asset"": ""USDT"", ""free"": ""8400.00000000"", ""locked"": ""1600.00000000"" }, { ""asset"": ""XRP"", ""free"": ""50000.00000000"", ""locked"": ""0.00000000"" } ], ""permissions"": [ ""SPOT"" ], ""uid"": 1688996631782681271 }";
        var account = new Account
        {
            Type = json.GetString("accountType"),
            ExternalAccount = json.GetLong("uid").ToString(),
            UpdateTime = json.GetLong("updateTime").FromUnixMs(),
        };
        var balanceArray = json["balances"]?.AsArray();
        if (balanceArray != null)
        {
            foreach (var balanceObj in balanceArray)
            {
                var asset = balanceObj.GetString("asset");
                var free = balanceObj.GetDecimal("free");
                var locked = balanceObj.GetDecimal("locked");
                var assetId = assets?.FirstOrDefault(a => a.Name == asset)?.Id ?? -1;
                if (assetId == -1)
                {
                    // those assets/symbols which are not actively trading
                    // this happens in TEST site very often
                    continue;
                }
                var balance = new Balance { AssetId = assetId, AssetCode = asset, FreeAmount = free, LockedAmount = locked };
                account.Balances.Add(balance);
            }
            // guarantee ordering
            account.Balances.Sort((x, y) => x.AssetCode.CompareTo(y.AssetCode));
        }
        return ExternalQueryStates.QueryAccounts(content, connId, account).RecordTimes(rtt, swOuter);
    }

    /// <summary>
    /// Get the account detailed info [SIGNED] [PROD ONLY].
    /// </summary>
    /// <returns></returns>
    public async Task<ExternalQueryState> GetAccounts()
    {
        // TODO parse logic missing
        var swTotal = Stopwatch.StartNew();
        var accounts = new List<Account>(_accountTypes.Count);
        var states = new List<ExternalQueryState>();
        ExternalQueryState? state = null;
        foreach (var accountType in _accountTypes)
        {
            var url = $"{_connectivity.RootUrl}/sapi/v1/accountSnapshot";
            var parameters = new List<(string, string)>
            {
                ("type", accountType),
                ("limit", 7.ToString()), // the api returns a history list of account statuses; min is 7
            };
            using var request = _requestBuilder.Build(HttpMethod.Get, url, parameters);

            var (response, rtt) = await _httpClient.TimedSendAsync(request);
            var connId = response.CheckHeaders();
            if (!response.ParseJsonObject(out var content, out var rootObj, out var errorMessage, _log))
                state = ExternalQueryStates.Error(ActionType.GetAccount, ResultCode.GetAccountFailed, content, connId, errorMessage);
            else
            {
                // TODO
                var account = Parse(rootObj);
                if (account != null)
                {
                    accounts.Add(account);
                    state = ExternalQueryStates.QueryAccounts(content, connId, account).RecordTimes(rtt);
                }
                else
                {
                    _log.Error($"Failed to get or parse account (type: {accountType}) response.");
                }
            }
            state ??= ExternalQueryStates.InvalidAccount(content, connId).RecordTimes(rtt);
            states.Add(state);
        }
        state = ExternalQueryStates.QueryAccounts(null, null, accounts.ToArray()).RecordTimes(swTotal);
        state.SubStates = states;

        return state;

        Account Parse(JsonObject rootObj) => throw new NotImplementedException();
    }
}
