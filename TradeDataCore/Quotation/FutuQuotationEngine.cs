using Futu.OpenApi;
using Futu.OpenApi.Pb;
using TradeCommon.Constants;
using TradeCommon.Essentials.Instruments;
using TradeCommon.Runtime;

namespace TradeDataCore.Quotation;

/// <summary>
/// Futu's quotation engine.
/// According to https://openapi.futunn.com/futu-api-doc/en/intro/authority.html#9123:
/// * For HK users, the cheapest real-time data quota is 100 simultaneous subscription;
/// * For HK users, the cheapest historical data quota is 100, depleted whenever a new
/// symbol is subscribed in the last 30 days. 
/// * For mainland China users, the LV2 HK market quotes and A-share LV1 market quotes are free.
/// </summary>
public class FutuQuotationEngine : IQuotationEngine
{
    private ConnectionProxy _connectionProxy;
    private QuoterProxy _quoterProxy;

    public FutuQuotationEngine()
    {
        var quoter = new FTAPI_Qot();
        _connectionProxy = new ConnectionProxy(quoter);
        _quoterProxy = new QuoterProxy(quoter);
    }

    public async Task<ExternalConnectionState> InitializeAsync()
    {
        return await _connectionProxy.ConnectAsync("127.0.0.1", 11111, false);
    }

    public async Task<ExternalConnectionState> SubscribeAsync(Security security)
    {
        return await _quoterProxy.SubscribeSecurityAsync(security);
    }

    public async Task<ExternalConnectionState> UnsubscribeAsync(Security security)
    {
        return await _quoterProxy.SubscribeSecurityAsync(security, false);
    }

    #region Connection Callbacks
    public class ConnectionProxy : FTSPI_Conn
    {
        private readonly TaskCompletionSource<ExternalConnectionState> _tcs1 = new();
        private readonly TaskCompletionSource<ExternalConnectionState> _tcs2 = new();
        private readonly FTAPI_Conn _api;

        public ConnectionProxy(FTAPI_Conn api)
        {
            _api = api;
            _api.SetConnCallback(this);
        }

        public async Task<ExternalConnectionState> ConnectAsync(string ip, int port, bool isEncryptionEnabled)
        {
            // TODO
            _api.SetClientInfo("csharp", 1);
            _api.InitConnect(ip, (ushort)port, isEncryptionEnabled);
            return await _tcs1.Task;
        }

        public async Task<ExternalConnectionState> CloseAsync()
        {
            _api.Close();
            return await _tcs2.Task;
        }

        public void OnInitConnect(FTAPI_Conn client, long errCode, string desc)
        {
            var result = new ExternalConnectionState
            {
                Type = SubscriptionType.QuotationService,
                Action = ConnectionActionType.Connect,
                StatusCode = errCode == 0 ? nameof(StatusCodes.ConnectionOk) : $"{nameof(StatusCodes.ConnectionFailed)},{errCode}",
                ExternalPartyId = ExternalNames.Futu,
                UniqueConnectionId = client.GetConnectID().ToString(),
                Description = desc,
            };
            _tcs1.SetResult(result);
        }

        public void OnDisconnect(FTAPI_Conn client, long errCode)
        {
            var result = new ExternalConnectionState
            {
                Type = SubscriptionType.QuotationService,
                Action = ConnectionActionType.Disconnect,
                StatusCode = errCode == 0 ? nameof(StatusCodes.DisconnectionOk) : $"{nameof(StatusCodes.DisconnectionFailed)},{errCode}",
                ExternalPartyId = ExternalNames.Futu,
                UniqueConnectionId = client.GetConnectID().ToString()
            };
            _tcs2.SetResult(result);
        }
    }
    #endregion

    #region Quotation Callbacks

    public class QuoterProxy : FTSPI_Qot
    {
        private FTAPI_Qot _quoter;

        private readonly TaskCompletionSource<ExternalConnectionState> _tcsSubscribeSecurity = new();

        public QuoterProxy(FTAPI_Qot quoter)
        {
            _quoter = quoter;
        }

        public async Task<ExternalConnectionState> SubscribeSecurityAsync(Security security, bool isSubscribe = true)
        {
            _quoter = _quoter ?? throw new InvalidOperationException("Must initialize quoter before security data subscription.");

            var market = security.Exchange switch
            {
                ExternalNames.Hkex when SecurityTypeConverter.Matches(security.Type, SecurityType.Equity)
                    => (int)QotCommon.QotMarket.QotMarket_HK_Security,
                ExternalNames.Hkex when SecurityTypeConverter.Matches(security.Type, SecurityType.Future)
                    => (int)QotCommon.QotMarket.QotMarket_HK_Future,
                _ => throw new NotImplementedException(),
            };

            var futuSecurity = QotCommon.Security.CreateBuilder()
                .SetMarket(market)
                .SetCode(Identifiers.ToFutuCode(security))
                .Build();
            var c2s = QotSub.C2S.CreateBuilder()
                .AddSecurityList(futuSecurity)
                .AddSubTypeList((int)QotCommon.SubType.SubType_Basic)
                .SetIsSubOrUnSub(isSubscribe)
                .Build();
            var request = QotSub.Request.CreateBuilder().SetC2S(c2s).Build();
            _quoter.Sub(request);

            return await _tcsSubscribeSecurity.Task;
        }

        public void OnReply_GetBasicQot(FTAPI_Conn client, uint nSerialNo, QotGetBasicQot.Response rsp)
        {
            throw new NotImplementedException();
        }

        public void OnReply_GetBroker(FTAPI_Conn client, uint nSerialNo, QotGetBroker.Response rsp)
        {
            throw new NotImplementedException();
        }

        public void OnReply_GetCapitalDistribution(FTAPI_Conn client, uint nSerialNo, QotGetCapitalDistribution.Response rsp)
        {
            throw new NotImplementedException();
        }

        public void OnReply_GetCapitalFlow(FTAPI_Conn client, uint nSerialNo, QotGetCapitalFlow.Response rsp)
        {
            throw new NotImplementedException();
        }

        public void OnReply_GetCodeChange(FTAPI_Conn client, uint nSerialNo, QotGetCodeChange.Response rsp)
        {
            throw new NotImplementedException();
        }

        public void OnReply_GetFutureInfo(FTAPI_Conn client, uint nSerialNo, QotGetFutureInfo.Response rsp)
        {
            throw new NotImplementedException();
        }

        public void OnReply_GetGlobalState(FTAPI_Conn client, uint nSerialNo, GetGlobalState.Response rsp)
        {
            throw new NotImplementedException();
        }

        public void OnReply_GetHoldingChangeList(FTAPI_Conn client, uint nSerialNo, QotGetHoldingChangeList.Response rsp)
        {
            throw new NotImplementedException();
        }

        public void OnReply_GetIpoList(FTAPI_Conn client, uint nSerialNo, QotGetIpoList.Response rsp)
        {
            throw new NotImplementedException();
        }

        public void OnReply_GetKL(FTAPI_Conn client, uint nSerialNo, QotGetKL.Response rsp)
        {
            throw new NotImplementedException();
        }

        public void OnReply_GetMarketState(FTAPI_Conn client, uint nSerialNo, QotGetMarketState.Response rsp)
        {
            throw new NotImplementedException();
        }

        public void OnReply_GetOptionChain(FTAPI_Conn client, uint nSerialNo, QotGetOptionChain.Response rsp)
        {
            throw new NotImplementedException();
        }

        public void OnReply_GetOptionExpirationDate(FTAPI_Conn client, uint nSerialNo, QotGetOptionExpirationDate.Response rsp)
        {
            throw new NotImplementedException();
        }

        public void OnReply_GetOrderBook(FTAPI_Conn client, uint nSerialNo, QotGetOrderBook.Response rsp)
        {
            throw new NotImplementedException();
        }

        public void OnReply_GetOwnerPlate(FTAPI_Conn client, uint nSerialNo, QotGetOwnerPlate.Response rsp)
        {
            throw new NotImplementedException();
        }

        public void OnReply_GetPlateSecurity(FTAPI_Conn client, uint nSerialNo, QotGetPlateSecurity.Response rsp)
        {
            throw new NotImplementedException();
        }

        public void OnReply_GetPlateSet(FTAPI_Conn client, uint nSerialNo, QotGetPlateSet.Response rsp)
        {
            throw new NotImplementedException();
        }

        public void OnReply_GetPriceReminder(FTAPI_Conn client, uint nSerialNo, QotGetPriceReminder.Response rsp)
        {
            throw new NotImplementedException();
        }

        public void OnReply_GetReference(FTAPI_Conn client, uint nSerialNo, QotGetReference.Response rsp)
        {
            throw new NotImplementedException();
        }

        public void OnReply_GetRT(FTAPI_Conn client, uint nSerialNo, QotGetRT.Response rsp)
        {
            throw new NotImplementedException();
        }

        public void OnReply_GetSecuritySnapshot(FTAPI_Conn client, uint nSerialNo, QotGetSecuritySnapshot.Response rsp)
        {
            throw new NotImplementedException();
        }

        public void OnReply_GetStaticInfo(FTAPI_Conn client, uint nSerialNo, QotGetStaticInfo.Response rsp)
        {
            throw new NotImplementedException();
        }

        public void OnReply_GetSubInfo(FTAPI_Conn client, uint nSerialNo, QotGetSubInfo.Response rsp)
        {
            // TODO
            var result = new ExternalConnectionState
            {
                Type = SubscriptionType.QuotationService,
                Action = ConnectionActionType.Subscribe,
                StatusCode = rsp.ErrCode == 0 ? nameof(StatusCodes.SubscriptionOk) : $"{nameof(StatusCodes.SubscriptionFailed)},{rsp.ErrCode}",
                ExternalPartyId = ExternalNames.Futu,
                UniqueConnectionId = nSerialNo.ToString(),
                Description = rsp.RetMsg,
            };
            _tcsSubscribeSecurity.SetResult(result);
        }

        public void OnReply_GetTicker(FTAPI_Conn client, uint nSerialNo, QotGetTicker.Response rsp)
        {
            throw new NotImplementedException();
        }

        public void OnReply_GetUserSecurity(FTAPI_Conn client, uint nSerialNo, QotGetUserSecurity.Response rsp)
        {
            throw new NotImplementedException();
        }

        public void OnReply_GetUserSecurityGroup(FTAPI_Conn client, uint nSerialNo, QotGetUserSecurityGroup.Response rsp)
        {
            throw new NotImplementedException();
        }

        public void OnReply_GetWarrant(FTAPI_Conn client, uint nSerialNo, QotGetWarrant.Response rsp)
        {
            throw new NotImplementedException();
        }

        public void OnReply_ModifyUserSecurity(FTAPI_Conn client, uint nSerialNo, QotModifyUserSecurity.Response rsp)
        {
            throw new NotImplementedException();
        }

        public void OnReply_Notify(FTAPI_Conn client, uint nSerialNo, Notify.Response rsp)
        {
            throw new NotImplementedException();
        }

        public void OnReply_RegQotPush(FTAPI_Conn client, uint nSerialNo, QotRegQotPush.Response rsp)
        {
            throw new NotImplementedException();
        }

        public void OnReply_RequestHistoryKL(FTAPI_Conn client, uint nSerialNo, QotRequestHistoryKL.Response rsp)
        {
            throw new NotImplementedException();
        }

        public void OnReply_RequestHistoryKLQuota(FTAPI_Conn client, uint nSerialNo, QotRequestHistoryKLQuota.Response rsp)
        {
            throw new NotImplementedException();
        }

        public void OnReply_RequestRehab(FTAPI_Conn client, uint nSerialNo, QotRequestRehab.Response rsp)
        {
            throw new NotImplementedException();
        }

        public void OnReply_RequestTradeDate(FTAPI_Conn client, uint nSerialNo, QotRequestTradeDate.Response rsp)
        {
            throw new NotImplementedException();
        }

        public void OnReply_SetPriceReminder(FTAPI_Conn client, uint nSerialNo, QotSetPriceReminder.Response rsp)
        {
            throw new NotImplementedException();
        }

        public void OnReply_StockFilter(FTAPI_Conn client, uint nSerialNo, QotStockFilter.Response rsp)
        {
            throw new NotImplementedException();
        }

        public void OnReply_Sub(FTAPI_Conn client, uint nSerialNo, QotSub.Response rsp)
        {
            throw new NotImplementedException();
        }

        public void OnReply_UpdateBasicQot(FTAPI_Conn client, uint nSerialNo, QotUpdateBasicQot.Response rsp)
        {
            throw new NotImplementedException();
        }

        public void OnReply_UpdateBroker(FTAPI_Conn client, uint nSerialNo, QotUpdateBroker.Response rsp)
        {
            throw new NotImplementedException();
        }

        public void OnReply_UpdateKL(FTAPI_Conn client, uint nSerialNo, QotUpdateKL.Response rsp)
        {
            throw new NotImplementedException();
        }

        public void OnReply_UpdateOrderBook(FTAPI_Conn client, uint nSerialNo, QotUpdateOrderBook.Response rsp)
        {
            throw new NotImplementedException();
        }

        public void OnReply_UpdatePriceReminder(FTAPI_Conn client, uint nSerialNo, QotUpdatePriceReminder.Response rsp)
        {
            throw new NotImplementedException();
        }

        public void OnReply_UpdateRT(FTAPI_Conn client, uint nSerialNo, QotUpdateRT.Response rsp)
        {
            throw new NotImplementedException();
        }

        public void OnReply_UpdateTicker(FTAPI_Conn client, uint nSerialNo, QotUpdateTicker.Response rsp)
        {
            throw new NotImplementedException();
        }
    }
    #endregion
}