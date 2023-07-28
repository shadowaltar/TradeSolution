using OfficeOpenXml.Style;
using System.Diagnostics;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json.Nodes;
using TradeCommon.Essentials.Quotes;

namespace Common;
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

    public static void Receive(this ClientWebSocket ws, Action<byte[]> parseResultFunc)
    {
        _ = Task.Factory.StartNew(async t =>
        {
            var buffer = new byte[1024];
            var bytes = new List<byte>();
            var result = await ws.ReceiveAsync(new ArraySegment<byte>(buffer), default);
            while (!result.CloseStatus.HasValue)
            {
                if (result.MessageType != WebSocketMessageType.Text)
                    continue;

                bytes.AddRange(buffer.Take(result.Count));

                if (!result.EndOfMessage)
                    continue;
                parseResultFunc.Invoke(bytes.ToArray());
                bytes.Clear();
                result = await ws.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
            }
        }, TaskCreationOptions.LongRunning, CancellationToken.None);
    }

    public static async Task Send(this ClientWebSocket ws, string payload)
    {
        if (ws.State != WebSocketState.Open)
            return;

        byte[] buffer = Encoding.UTF8.GetBytes(payload);

        await ws.SendAsync(new ArraySegment<byte>(buffer), WebSocketMessageType.Binary, false, CancellationToken.None);

    }
}
