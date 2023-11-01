using System.Diagnostics;
using System.Net;

namespace TradeCommon.Externals;
public class FakeHttpClient : HttpClient
{
    public string Content { get; set; }
    public List<(string, string)> Headers { get; } = new();

    public async Task<(HttpResponseMessage response, long elapsedMs)> TimedSendAsync(HttpRequestMessage request)
    {
        var swInner = Stopwatch.StartNew();
        var response = await SendAsync(request);
        swInner.Stop();
        return (response, swInner.ElapsedMilliseconds);
    }

    public new async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request)
    {
        var response = new HttpResponseMessage(HttpStatusCode.OK);
        foreach (var header in Headers)
        {
            response.Headers.Add(header.Item1, header.Item2);
        }
        response.Content = new FakeHttpContent() { HardcodeContent = Content };
        return response;
    }

}
