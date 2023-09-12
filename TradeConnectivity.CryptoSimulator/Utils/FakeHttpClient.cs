using CsvHelper.Configuration.Attributes;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace TradeConnectivity.CryptoSimulator.Utils;
public class FakeHttpClient : HttpClient
{
    public string Content { get; set; }
    public List<(string, string)> Headers { get; } = new();

    public async Task<(HttpResponseMessage response, long elapsedMs)> TimedSendAsync(HttpRequestMessage request)
    {
        var swInner = Stopwatch.StartNew();
        var response = new HttpResponseMessage(HttpStatusCode.OK);
        foreach (var header in Headers)
        {
            response.Headers.Add(header.Item1, header.Item2);
        }
        response.Content = new FakeHttpContent(Content);
        swInner.Stop();
        return (response, swInner.ElapsedMilliseconds);
    }
}

public class FakeHttpContent : HttpContent
{
    private string _hardcodeContent;

    public FakeHttpClient(string hardcodeContent)
    {
        _hardcodeContent = hardcodeContent;
    }

    protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context)
    {
        throw new NotImplementedException();
    }

    protected override bool TryComputeLength(out long length)
    {
        throw new NotImplementedException();
    }

    public async Task<string> ReadAsStringAsync()
    {
        return _hardcodeContent;
    }
}