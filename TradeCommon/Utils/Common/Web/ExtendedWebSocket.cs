using log4net;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.WebSockets;
using System.Security.Policy;
using System.Text;
using System.Threading.Tasks;

namespace Common.Web;

public class ExtendedWebSocket : IDisposable
{
    private ClientWebSocket? _webSocket;
    private readonly ILog _log;
    private bool _isRunning = true;

    public ExtendedWebSocket(ILog log)
    {
        _log = log;
    }

    public void Listen(Uri uri, Action<byte[]> parseResultCallback, Action webSocketCreatedCallback)
    {
        Task.Factory.StartNew(async t =>
        {
            do
            {
                if (!_isRunning)
                    return;

                _webSocket = new();
                try
                {
                    await _webSocket.ConnectAsync(uri, default);
                    webSocketCreatedCallback.Invoke();

                    var buffer = new byte[1024];
                    var bytes = new List<byte>();
                    var result = await _webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), default);
                    while (!result.CloseStatus.HasValue)
                    {
                        if (!_isRunning)
                            return;

                        if (result.MessageType != WebSocketMessageType.Text)
                            continue;

                        bytes.AddRange(buffer.Take(result.Count));

                        if (!result.EndOfMessage)
                            continue;

                        if (!_isRunning)
                            return;

                        parseResultCallback.Invoke(bytes.ToArray());
                        bytes.Clear();
                        result = await _webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
                    }
                }
                catch (WebSocketException e)
                {
                    _log.Error("Web socket is faulted. Exception message: " + e.Message);
                }
                finally
                {
                    _webSocket?.Dispose();
                }

                _log.Info("Trying to reconnect web socket.");
            } while (true);
        }, TaskCreationOptions.LongRunning, CancellationToken.None);
    }

    public async Task Send(Uri uri, string payload)
    {
        _webSocket ??= new();
        await _webSocket.ConnectAsync(uri, default);
        if (_webSocket.State != WebSocketState.Open)
            return;

        byte[] buffer = Encoding.UTF8.GetBytes(payload);

        await _webSocket.SendAsync(new ArraySegment<byte>(buffer), WebSocketMessageType.Binary, false, CancellationToken.None);
    }

    public async Task Close()
    {
        if (_webSocket != null)
            await _webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Client Closed", default);
    }

    public void Dispose()
    {
        _isRunning = false;
        _webSocket?.Dispose();
    }

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
