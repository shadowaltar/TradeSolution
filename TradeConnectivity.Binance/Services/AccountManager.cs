using Common;
using log4net;
using System.Diagnostics;
using System.Text.Json.Nodes;
using TradeCommon.Constants;
using TradeCommon.Essentials.Accounts;
using TradeCommon.Essentials.Portfolios;
using TradeCommon.Externals;
using TradeCommon.Runtime;
using TradeConnectivity.Binance.Utils;

namespace TradeConnectivity.Binance.Services;
public class AccountManager : IExternalAccountManagement
{
    private static readonly ILog _log = Logger.New();
    private static readonly List<string> _accountTypes = new() { "SPOT", "MARGIN", "FUTURES" };

    private readonly HttpClient _httpClient;
    private readonly RequestBuilder _requestBuilder;

    public AccountManager(HttpClient httpClient, KeyManager keyManager)
    {
        _httpClient = httpClient;
        _requestBuilder = new RequestBuilder(keyManager, Constants.ReceiveWindowMsString);
    }

    /// <summary>
    /// Get the account information [SIGNED].
    /// </summary>
    /// <returns></returns>
    public async Task<ExternalQueryState<Account?>> GetAccount()
    {
        var swOuter = Stopwatch.StartNew();
        var url = $"{RootUrls.DefaultHttps}/api/v3/account";
        using var request = new HttpRequestMessage();
        _requestBuilder.Build(request, HttpMethod.Get, url, true);

        var swInner = Stopwatch.StartNew();
        var response = await _httpClient.SendAsync(request);
        swInner.Stop();
        // example json: var responseJson = @"{ ""makerCommission"": 0, ""takerCommission"": 0, ""buyerCommission"": 0, ""sellerCommission"": 0, ""commissionRates"": { ""maker"": ""0.00000000"", ""taker"": ""0.00000000"", ""buyer"": ""0.00000000"", ""seller"": ""0.00000000"" }, ""canTrade"": true, ""canWithdraw"": false, ""canDeposit"": false, ""brokered"": false, ""requireSelfTradePrevention"": false, ""preventSor"": false, ""updateTime"": 1690995029309, ""accountType"": ""SPOT"", ""balances"": [ { ""asset"": ""BNB"", ""free"": ""1000.00000000"", ""locked"": ""0.00000000"" }, { ""asset"": ""BTC"", ""free"": ""1.00000000"", ""locked"": ""0.00000000"" }, { ""asset"": ""BUSD"", ""free"": ""10000.00000000"", ""locked"": ""0.00000000"" }, { ""asset"": ""ETH"", ""free"": ""100.00000000"", ""locked"": ""0.00000000"" }, { ""asset"": ""LTC"", ""free"": ""500.00000000"", ""locked"": ""0.00000000"" }, { ""asset"": ""TRX"", ""free"": ""500000.00000000"", ""locked"": ""0.00000000"" }, { ""asset"": ""USDT"", ""free"": ""8400.00000000"", ""locked"": ""1600.00000000"" }, { ""asset"": ""XRP"", ""free"": ""50000.00000000"", ""locked"": ""0.00000000"" } ], ""permissions"": [ ""SPOT"" ], ""uid"": 1688996631782681271 }";
        var responseString = await CheckContentAndStatus(response);

        Account? account = null;
        var json = JsonNode.Parse(responseString);
        if (json != null && responseString != "" && responseString != "{}")
        {
            account = new();
            var accountType = json.GetString("accountType");
            var externalAccount = json.GetLong("uid").ToString();
            account.Type = accountType;
            account.ExternalAccount = externalAccount;
            var balanceArray = json["balances"]?.AsArray();
            if (balanceArray != null)
            {
                foreach (var balanceObj in balanceArray)
                {
                    var asset = balanceObj.GetString("asset");
                    var free = balanceObj.GetDecimal("free");
                    var locked = balanceObj.GetDecimal("locked");
                    var balance = new Balance { AssetName = asset, FreeAmount = free, LockedAmount = locked };
                    account.Balances.Add(balance);
                }
            }
        }
        if (account != null)
        {
            var state = new ExternalQueryState<Account?>
            {
                Content = account,
                ResponsePayload = responseString,
                Action = ExternalActionType.GetAccount,
                ExternalPartyId = ExternalNames.Binance,
                StatusCode = StatusCodes.GetAccountOk,
                UniqueConnectionId = ResponseHandler.GetUniqueConnectionId(response),
                Description = "Get account info",
                NetworkRoundtripTime = swInner.ElapsedMilliseconds,
            };
            swOuter.Stop();
            state.TotalTime = swOuter.ElapsedMilliseconds;
            return state;
        }
        else
        {
            var state = new ExternalQueryState<Account?>
            {
                Content = null,
                ResponsePayload = responseString,
                Action = ExternalActionType.GetAccount,
                ExternalPartyId = ExternalNames.Binance,
                StatusCode = StatusCodes.GetAccountFailed,
                UniqueConnectionId = ResponseHandler.GetUniqueConnectionId(response),
                Description = "Failed to get account info",
                NetworkRoundtripTime = swInner.ElapsedMilliseconds,
            };
            swOuter.Stop();
            state.TotalTime = swOuter.ElapsedMilliseconds;
            return state;
        }
    }

    /// <summary>
    /// Get the account detailed info [SIGNED] [PROD ONLY].
    /// </summary>
    /// <returns></returns>
    public async Task<ExternalQueryState<List<Account>>> GetAccounts()
    {
        var accounts = new List<Account>(_accountTypes.Count);
        var payloads = new List<string>(_accountTypes.Count);
        var connIds = new List<string>(_accountTypes.Count);
        var swHttpRoundtrips = new List<long>(_accountTypes.Count);
        var isOk = true;
        var swTotal = Stopwatch.StartNew();
        foreach (var accountType in _accountTypes)
        {
            var url = $"{RootUrls.DefaultHttps}/sapi/v1/accountSnapshot";
            using var request = new HttpRequestMessage();
            var parameters = new List<(string, string)>
            {
                ("type", accountType),
                ("limit", 7.ToString()), // the api returns a history list of account statuses; min is 7
            };
            var payload = _requestBuilder.Build(request, HttpMethod.Get, url, true, parameters);
            payloads.Add(payload);
            var swHttpRoundtrip = Stopwatch.StartNew();
            var response = await _httpClient.SendAsync(request);
            swHttpRoundtrip.Stop();
            swHttpRoundtrips.Add(swHttpRoundtrip.ElapsedMilliseconds);

            var responseString = await CheckContentAndStatus(response);
            ResponseHandler.CheckHeaders(response);

            var rootObj = JsonNode.Parse(responseString)?.AsObject();
            if (rootObj != null && responseString != "" && responseString != "{}")
            {
                var account = Parse(rootObj);
                if (account != null)
                {
                    accounts.Add(account);
                }
                else
                {
                    _log.Error($"Failed to get or parse account (type: {accountType}) response.");
                    isOk = false;
                }
            }
            connIds.Add(ResponseHandler.GetUniqueConnectionId(response));
        }

        var state = new ExternalQueryState<List<Account>>
        {
            Content = accounts,
            ResponsePayload = $"[{string.Join(Environment.NewLine, payloads)}]",
            Action = ExternalActionType.CancelOrder,
            ExternalPartyId = ExternalNames.Binance,
            StatusCode = isOk ? StatusCodes.GetAccountOk : StatusCodes.GetAccountFailed,
            UniqueConnectionId = string.Join(",", connIds),
            Description = "Retrieved accounts",
        };

        swTotal.Stop();
        state.NetworkRoundtripTime = Convert.ToInt64(swHttpRoundtrips.Average());
        state.TotalTime = swTotal.ElapsedMilliseconds;

        return state;

        Account Parse(JsonObject rootObj) => throw new NotImplementedException();
    }

    /// <summary>
    /// Check the <see cref="HttpResponseMessage"/> responseString and status.
    /// Should be called after any http request.
    /// </summary>
    /// <param name="response"></param>
    /// <returns></returns>
    private static async Task<string> CheckContentAndStatus(HttpResponseMessage response)
    {
        var content = await response.Content.ReadAsStringAsync();
        if (response.IsSuccessStatusCode)
        {
            _log.Info(response);
        }
        else
        {
            _log.Error(response.StatusCode + ": " + content);
        }
        return content;
    }
}
