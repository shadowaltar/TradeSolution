using System.Diagnostics;
using System.Net.WebSockets;
using System.Text;

namespace TradeCommon.Utils.Common;
public static class WebSocketHelper
{
    public static async Task<string?> ListenOne(string url, int timeoutMs = 10000)
    {
        Uri uri = new(url);

        using ClientWebSocket ws = new();
        await ws.ConnectAsync(uri, default);

        var buffer = new byte[1024];
        var bytes = new List<byte>();
        var result = await ws.ReceiveAsync(new ArraySegment<byte>(buffer), default);
        var sw = Stopwatch.StartNew();
        while (!result.CloseStatus.HasValue || sw.ElapsedMilliseconds > timeoutMs)
        {
            if (result.MessageType == WebSocketMessageType.Text)
            {
                bytes.AddRange(buffer.Take(result.Count));

                if (result.EndOfMessage)
                {
                    string message = Encoding.UTF8.GetString(bytes.ToArray(), 0, bytes.Count);

                    await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "Client Closed", default);

                    return message;
                }
            }
        }
        return null;
    }
}
