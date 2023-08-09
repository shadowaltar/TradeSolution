using Azure;
using Common;
using log4net;
using OfficeOpenXml.Style;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using TradeCommon.Constants;
using TradeCommon.Essentials;
using TradeCommon.Essentials.Trading;
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
    /// Get the account detailed info [SIGNED]
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
                ("limit", 1.ToString()), // the api returns a history list of account statuses, only get the latest one here
            };
            var payload = _requestBuilder.Build(request, HttpMethod.Delete, url, true, parameters);
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

        Account Parse(JsonObject rootObj)
        {
            throw new NotImplementedException();
        }
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
