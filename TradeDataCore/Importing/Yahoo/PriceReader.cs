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
    /// String key here is the yahoo symbol. Format usually look like "xxxxxx.SZ" (or "SS") for SZ/SH exchanges, or "xxxx.HK" for HKEX.
    /// </summary>
    /// <param name="asOfTime"></param>
    /// <param name="tickers"></param>
    /// <returns></returns>
    public async Task<IDictionary<string, List<OhlcPrice>>> ReadYahooPrices(List<string> tickers, IntervalType interval,
         DateTime startTime, DateTime endTime)
    {
        return await ReadYahooPrices(tickers, interval, startTime, endTime, TimeRangeType.Unknown);
    }

    /// <summary>
    /// String key here is the yahoo symbol. Format usually look like "xxxxxx.SZ" (or "SS") for SZ/SH exchanges, or "xxxx.HK" for HKEX.
    /// </summary>
    /// <param name="asOfTime"></param>
    /// <param name="tickers"></param>
    /// <returns></returns>
    public async Task<IDictionary<string, List<OhlcPrice>>> ReadYahooPrices(List<string> tickers, IntervalType interval,
         TimeRangeType range)
    {
        return await ReadYahooPrices(tickers, interval, null, null, range);
    }

    private async Task<IDictionary<string, List<OhlcPrice>>> ReadYahooPrices(List<string> tickers, IntervalType interval,
        DateTime? start, DateTime? end, TimeRangeType range)
    {
        var results = new ConcurrentDictionary<string, List<OhlcPrice>>();
        if (range == TimeRangeType.Unknown && (start == null || end == null))
        {
            throw new InvalidOperationException("Range must be set, or provide both start and end time.");
        }

        using var httpClient = new HttpClient();

        var buckets = tickers.Split(200);
        ParallelOptions parallelOptions = new() { MaxDegreeOfParallelism = 50 };
        foreach (var bucket in buckets)
        {
            await Parallel.ForEachAsync(bucket, parallelOptions, async (ticker, ct) =>
            {
                var formattedTicker = Uri.EscapeDataString(ticker);
                var intervalStr = IntervalTypeConverter.ToYahooIntervalString(interval);

                List<OhlcPrice>? prices = null;
                if (range == TimeRangeType.Unknown && start != null && end != null)
                {
                    const string urlTemplate = "https://query1.finance.yahoo.com/v8/finance/chart/{0}?interval={1}&period1={2}&period2={3}&events=div%7Csplit";

                    var startTs = start.Value.ToUnixSec();
                    var endTs = end.Value.ToUnixSec();
                    var url = string.Format(urlTemplate, formattedTicker, intervalStr, startTs, endTs);
                    _log.Info($"Downloading prices for {ticker} [{intervalStr}] {start:yyMMdd}->{end:yyMMdd}; URL: {url}");
                    prices = await InternalReadYahooPrices(httpClient, ticker, interval, url);
                }
                else if (range != TimeRangeType.Unknown)
                {
                    const string urlTemplate = "https://query1.finance.yahoo.com/v8/finance/chart/{0}?interval={1}&range={2}&events=div%7Csplit";

                    var rangeStr = TimeRangeTypeConverter.ToYahooIntervalString(range);
                    var url = string.Format(urlTemplate, formattedTicker, intervalStr, rangeStr);
                    _log.Info($"Downloading prices for {ticker} [{intervalStr}] {rangeStr}->Now; URL: {url}");
                    prices = await InternalReadYahooPrices(httpClient, ticker, interval, url);
                }
                if (prices != null)
                {
                    results[ticker] = prices;
                }
            });
            Thread.Sleep(1000);
        }
        return results;
    }

    private static async Task<List<OhlcPrice>?> InternalReadYahooPrices(HttpClient httpClient, string ticker, IntervalType interval, string url)
    {
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
                var adjustedCloses = priceRootObj!["adjclose"]!.AsArray()[0]!["adjclose"]!
                    .AsArray().Select(n => (decimal)(n ?? 0m).AsValue()).ToArray();

                // TODO
                var corporateActionsObj = rootObj["events"];
                if (corporateActionsObj != null)
                {
                    var splitsObj = corporateActionsObj["splits"];
                    var dividendsObj = corporateActionsObj["dividends"];
                }
            }
            var quoteRootObj = priceRootObj["quote"]![0]!;
            if (timeRootObj == null)
            {
                _log.Info($"Ticker {ticker} has no market price data at all.");
                return null;
            }
            var localStartTimes = timeRootObj!.AsArray().Select(n =>
                DateTimeOffset.FromUnixTimeSeconds((int)n!.AsValue())
                .ToLocalTime().DateTime).ToArray();
            var highs = quoteRootObj["high"]!.AsArray().Select(n => (decimal)(n ?? 0m).AsValue()).ToArray();
            var lows = quoteRootObj["low"]!.AsArray().Select(n => (decimal)(n ?? 0m).AsValue()).ToArray();
            var opens = quoteRootObj["open"]!.AsArray().Select(n => (decimal)(n ?? 0m).AsValue()).ToArray();
            var closes = quoteRootObj["close"]!.AsArray().Select(n => (decimal)(n ?? 0m).AsValue()).ToArray();
            var volumes = quoteRootObj["volume"]!.AsArray().Select(n => (decimal)(n ?? 0m).AsValue()).ToArray();
            var prices = new List<OhlcPrice>();
            for (var i = 0; i < localStartTimes.Length; i++)
            {
                // TODO hardcode assumption: prices only have 2 dp; vol has 0.
                opens[i] = Math.Round(opens[i], 2, MidpointRounding.ToEven);
                highs[i] = Math.Round(highs[i], 2, MidpointRounding.ToEven);
                lows[i] = Math.Round(lows[i], 2, MidpointRounding.ToEven);
                closes[i] = Math.Round(closes[i], 2, MidpointRounding.ToEven);
                volumes[i] = Math.Round(closes[i], MidpointRounding.ToEven);
                var price = new OhlcPrice(opens[i], highs[i], lows[i], closes[i], volumes[i], localStartTimes[i]);
                prices.Add(price);
            }
            return prices;
        }
        catch (Exception ex)
        {
            _log.Error($"Error when downloading prices for {ticker}.", ex);
            return null;
        }
    }
}
