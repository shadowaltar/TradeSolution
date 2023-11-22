using Common;
using log4net;
using System.Diagnostics;
using TradeCommon.Externals;
using TradeCommon.Runtime;
using TradeCommon.Utils;
using TradeConnectivity.Binance.Utils;

namespace TradeConnectivity.Binance.Services;

public class Connectivity : IExternalConnectivityManagement
{
    private static readonly ILog _log = Logger.New();

    private readonly string _prodHttps = "https://api.binance.com";
    private readonly string _prodWs = "wss://stream.binance.com:9443";

    private readonly string _testHttps = "https://testnet.binance.vision";
    private readonly string _testWs = "wss://testnet.binance.vision";

    private readonly string _localHttps = "https://localhost";
    private readonly string _localWs = "wss://localhost";

    private readonly RequestBuilder _requestBuilder;
    private readonly HttpClient _httpClient;
    private readonly Stopwatch _stopwatch;

    private string _pingUrl;

    public string RootUrl { get; protected set; }

    public string RootWebSocketUrl { get; protected set; }

    public Connectivity(ApplicationContext context,
                        HttpClient httpClient,
                        KeyManager keyManager)
    {
        _requestBuilder = new RequestBuilder(keyManager, Constants.ReceiveWindowMsString);
        _httpClient = httpClient;
        _stopwatch = new Stopwatch();

        if (context.IsExternalProhibited)
        {
            _prodHttps = "https://not.exist";
            _prodWs = "wss://not.exist:9443";

            _testHttps = "https://test.not.exist";
            _testWs = "wss://test.not.exist";
        }

        SetEnvironment(EnvironmentType.Test);
    }

    public void SetEnvironment(EnvironmentType environment)
    {
        switch (environment)
        {
            case EnvironmentType.Test:
            case EnvironmentType.Uat:
                RootUrl = _testHttps;
                RootWebSocketUrl = _testWs;
                break;
            case EnvironmentType.Prod:
                RootUrl = _prodHttps;
                RootWebSocketUrl = _prodWs;
                break;
            case EnvironmentType.Unknown:
                RootUrl = _localHttps;
                RootWebSocketUrl = _localWs;
                break;
            default: throw new ArgumentException("Invalid environment type", nameof(environment));
        }
        _pingUrl = $"{RootUrl}/api/v3/ping";
    }

    /// <summary>
    /// Ping the external server.
    /// </summary>
    /// <returns></returns>
    public bool Ping()
    {
        if (!Firewall.CanCall)
            return true;

        try
        {
            _stopwatch.Restart();
            using var pingRequest = _requestBuilder.Build(HttpMethod.Get, _pingUrl);

            var response = _httpClient.Send(pingRequest);
            _stopwatch.Stop();
            if (response.StatusCode != System.Net.HttpStatusCode.OK)
            {
                _log.Error($"[{_stopwatch.Elapsed.TotalSeconds:F4}s][{(int)response.StatusCode} {response.ReasonPhrase}] Connection to external server is probably lost.");
                return false;
            }
            if (_log.IsDebugEnabled)
                _log.Debug($"[{_stopwatch.Elapsed.TotalSeconds:F4}s] Pinged external server.");
            return true;
        }
        catch (Exception e)
        {
            _log.Error("Ping time out; please try again later.", e);
            return false;
        }
    }

    public double GetAverageLatency()
    {
        var milliseconds = new double[10];
        Parallel.For(0, 10, i =>
        {
            try
            {
                var sw = new Stopwatch();
                using var pingRequest = _requestBuilder.Build(HttpMethod.Get, _pingUrl);

                sw.Start();
                var response = _httpClient.Send(pingRequest);
                sw.Stop();
                milliseconds[i] = sw.Elapsed.TotalMilliseconds;
            }
            catch
            {
                // silently ignore
            }
        });
        var average = milliseconds.Where(ms => ms < 10000).Average();
        _log.Info("Discovered average RTT in ms: " + average);
        return average;
    }
}
