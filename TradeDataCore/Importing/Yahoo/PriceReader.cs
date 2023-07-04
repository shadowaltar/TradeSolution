using log4net;
using System.Collections.Concurrent;
using System.Text.Json.Nodes;
using TradeDataCore.Essentials;
using TradeDataCore.Utils;

namespace TradeDataCore.Importing.Yahoo;

public class PriceReader
{
    private static readonly ILog _log = LogManager.GetLogger(typeof(PriceReader));

    /// <summary>
    /// String key here is the yahoo symbol. Format usually look like "xxxxxx.SZ" (or "SS") for SZ/SH exchanges.
    /// </summary>
    /// <param name="asOfTime"></param>
    /// <param name="tickers"></param>
    /// <returns></returns>
    public async Task<IDictionary<string, List<OhlcPrice>>> ReadYahooPrices(List<string> tickers, IntervalType interval, DateTime startTime, DateTime endTime)
    {
        var results = new ConcurrentDictionary<string, List<OhlcPrice>>();

        using var httpClient = new HttpClient();
        var startDto = new DateTimeOffset(startTime, TimeSpan.Zero);
        var endDto = new DateTimeOffset(endTime, TimeSpan.Zero);

        var buckets = tickers.Split(200);
        ParallelOptions parallelOptions = new() { MaxDegreeOfParallelism = 50 };
        foreach (var bucket in buckets)
        {
            await Parallel.ForEachAsync(bucket, parallelOptions, async (ticker, ct) =>
            {
                var prices = await ReadYahooPrices(httpClient, ticker, interval, startDto, endDto);
                if (prices != null)
                {
                    results[ticker] = prices;
                }
            });
            Thread.Sleep(1000);
        }
        return results;
    }

    private static async Task<List<OhlcPrice>?> ReadYahooPrices(HttpClient httpClient, string ticker, IntervalType interval, DateTimeOffset startDto, DateTimeOffset endDto)
    {
        const string urlTemplate = "https://query1.finance.yahoo.com/v8/finance/chart/{0}?interval={1}&period1={2}&period2={3}";

        var formattedTicker = Uri.EscapeDataString(ticker);

        var intervalStr = IntervalTypeConverter.ToYahooIntervalString(interval);
        var startTs = startDto.ToUnixTimeSeconds();
        var endTs = endDto.ToUnixTimeSeconds();

        var url = string.Format(urlTemplate, formattedTicker, intervalStr, startTs, endTs);
        string json;
        try
        {
            json = await httpClient.GetStringAsync(url);
        }
        catch (Exception e)
        {
            _log.Error($"Yahoo price does not exist for ticker {ticker}. Url: {url}", e);
            return null;
        }

        var jo = JsonNode.Parse(json)?.AsObject();
        if (jo == null)
            return null;

        try
        {
            var rootObj = jo["chart"]!["result"]!.AsArray()[0]!;
            var priceRootObj = rootObj["indicators"]!;
            var timeRootObj = rootObj["timestamp"]!;
            if (interval == IntervalType.OneDay)
            {
                // TODO useless for now
                var adjustedClose = priceRootObj!["adjclose"]!.AsArray()[0]!["adjclose"];
            }
            var quoteRootObj = priceRootObj["quote"]![0]!;
            var times = timeRootObj!.AsArray().Select(n => DateTimeOffset.FromUnixTimeSeconds((int)n!.AsValue())).ToArray();
            var highs = quoteRootObj["high"]!.AsArray().Select(n => (decimal)n!.AsValue()).ToArray();
            var lows = quoteRootObj["low"]!.AsArray().Select(n => (decimal)n!.AsValue()).ToArray();
            var opens = quoteRootObj["open"]!.AsArray().Select(n => (decimal)n!.AsValue()).ToArray();
            var closes = quoteRootObj["close"]!.AsArray().Select(n => (decimal)n!.AsValue()).ToArray();
            var volumes = quoteRootObj["volume"]!.AsArray().Select(n => (decimal)n!.AsValue()).ToArray();
            var prices = new List<OhlcPrice>();
            for (var i = 0; i < times.Length; i++)
            {
                // TODO hardcode assumption: prices only have 2 dp; vol has 0.
                opens[i] = Math.Round(opens[i], 2, MidpointRounding.ToEven);
                highs[i] = Math.Round(highs[i], 2, MidpointRounding.ToEven);
                lows[i] = Math.Round(lows[i], 2, MidpointRounding.ToEven);
                closes[i] = Math.Round(closes[i], 2, MidpointRounding.ToEven);
                volumes[i] = Math.Round(closes[i], MidpointRounding.ToEven);
                var price = new OhlcPrice(opens[i], highs[i], lows[i], closes[i], volumes[i], times[i]);
                prices.Add(price);
            }
            _log.Info($"Downloaded prices for {ticker} {startDto:yyMMdd}->{endDto:yyMMdd}");
            return prices;
        }
        catch (Exception ex)
        {
            _log.Error($"Error when downloading prices for {ticker} {startDto:yyMMdd}->{endDto:yyMMdd}", ex);
            return null;
        }
    }

}
