using Futu.OpenApi;
using TradeCommon.Constants;
using TradeCommon.Runtime;

namespace TradeConnectivity.Futu.Proxies;

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
        _api.SetClientInfo("TradingUnicorn", 1);
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
            Action = ExternalActionType.Connect,
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
            Action = ExternalActionType.Disconnect,
            StatusCode = errCode == 0 ? nameof(StatusCodes.DisconnectionOk) : $"{nameof(StatusCodes.DisconnectionFailed)},{errCode}",
            ExternalPartyId = ExternalNames.Futu,
            UniqueConnectionId = client.GetConnectID().ToString()
        };
        _tcs2.SetResult(result);
    }
}