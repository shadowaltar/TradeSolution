using log4net;
using System.Diagnostics;
using System.Net.WebSockets;
using System.Text;

namespace Common;
public static class WebSocketHelper
{
    private static readonly ILog _log = Logger.New();

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

    /// <summary>
    /// Listen to a URI using web-socket.
    /// </summary>
    /// <param name="uri"></param>
    /// <param name="parseResultFunc"></param>
    /// <returns></returns>
    public static void Listen(this Uri uri, Action<byte[]> parseResultFunc, Action<ClientWebSocket> webSocketCreatedFunc)
    {
        ClientWebSocket ws = new();
        Task.Factory.StartNew(async t =>
        {
            do
            {
                ws = new();
                try
                {
                    await ws.ConnectAsync(uri, default);
                    webSocketCreatedFunc.Invoke(ws);

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
                }
                catch (WebSocketException e)
                {
                    _log.Error("Web socket is faulted. Exception message: " + e.Message, e);
                }
                finally
                {
                    ws?.Dispose();
                }
            } while (true);
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
