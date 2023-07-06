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
    public async Task<IDictionary<string, PricesAndCorporateActions>> ReadYahooPrices(List<string> tickers, IntervalType interval,
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
    public async Task<IDictionary<string, PricesAndCorporateActions>> ReadYahooPrices(List<string> tickers, IntervalType interval,
         TimeRangeType range)
    {
        return await ReadYahooPrices(tickers, interval, null, null, range);
    }

    private static async Task<IDictionary<string, PricesAndCorporateActions>> ReadYahooPrices(List<string> tickers, IntervalType interval,
        DateTime? start, DateTime? end, TimeRangeType range)
    {
        var results = new ConcurrentDictionary<string, PricesAndCorporateActions>();
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

                PricesAndCorporateActions? tuple = null;
                if (range == TimeRangeType.Unknown && start != null && end != null)
                {
                    const string urlTemplate = "https://query1.finance.yahoo.com/v8/finance/chart/{0}?interval={1}&period1={2}&period2={3}&events=div%7Csplit";

                    var startTs = start.Value.ToUnixSec();
                    var endTs = end.Value.ToUnixSec();
                    var url = string.Format(urlTemplate, formattedTicker, intervalStr, startTs, endTs);
                    _log.Info($"Downloading prices for {ticker} [{intervalStr}] {start:yyMMdd}->{end:yyMMdd}; URL: {url}");
                    tuple = await InternalReadYahooPrices(httpClient, ticker, interval, url);
                }
                else if (range != TimeRangeType.Unknown)
                {
                    const string urlTemplate = "https://query1.finance.yahoo.com/v8/finance/chart/{0}?interval={1}&range={2}&events=div%7Csplit";

                    var rangeStr = TimeRangeTypeConverter.ToYahooIntervalString(range);
                    var url = string.Format(urlTemplate, formattedTicker, intervalStr, rangeStr);
                    _log.Info($"Downloading prices for {ticker} [{intervalStr}] {rangeStr}->Now; URL: {url}");
                    tuple = await InternalReadYahooPrices(httpClient, ticker, interval, url);
                }

                if (tuple != null)
                {
                    results[ticker] = tuple!;
                }
            });
            Thread.Sleep(1000);
        }
        return results;
    }

    private static async Task<PricesAndCorporateActions?> InternalReadYahooPrices(HttpClient httpClient,
        string ticker, IntervalType interval, string url)
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
            var corporateActions = new List<IStockCorporateAction>();
            if (interval == IntervalType.OneDay)
            {
                // TODO useless for now
                var adjustedCloses = priceRootObj!["adjclose"]!.AsArray()[0]!["adjclose"]!
                    .AsArray().Select(n => (decimal)(n ?? 0m).AsValue()).ToArray();

                // TODO
                var corporateActionsObj = rootObj["events"];
                if (corporateActionsObj != null)
                {
                    var splitsObj = corporateActionsObj["splits"]?.AsObject();
                    var dividendsObj = corporateActionsObj["dividends"]?.AsObject();
                    if (splitsObj != null)
                    {
                        foreach (var splitObj in splitsObj)
                        {
                            var payableDate = splitObj.Key?.ParseLocalUnixDate() ?? default;
                            var contentObj = splitObj.Value;
                            var numerator = contentObj.GetDecimal("numerator");
                            var denominator = contentObj.GetDecimal("denominator");
                            var exDate = contentObj.GetLocalUnixDateTime("date");
                            if (payableDate == default || numerator == default || denominator == default || exDate == default)
                            {
                                _log.Warn($"Invalid stock split entry. Payable:{payableDate:yyyyMMdd},Ex:{exDate:yyyyMMdd},N/D:{numerator}/{denominator}");
                                continue;
                            }

                            corporateActions ??= new List<IStockCorporateAction>();
                            corporateActions.Add(new StockSplitEvent(payableDate, exDate, (int)numerator, (int)denominator));
                        }
                    }
                    if (dividendsObj != null)
                    {
                        foreach (var dividendObj in dividendsObj)
                        {
                            var exDate = dividendObj.Key?.ParseLocalUnixDate() ?? default;
                            var contentObj = dividendObj.Value;
                            var amount = contentObj.GetDecimal("amount");
                            var paymentDate = contentObj.GetLocalUnixDateTime("date");
                            if (exDate == default || amount == default || paymentDate == default)
                            {
                                _log.Warn($"Invalid dividend entry. Ex:{exDate:yyyyMMdd},Payment:{paymentDate:yyyyMMdd},Amt:{amount}");
                                continue;
                            }

                            corporateActions ??= new List<IStockCorporateAction>();
                            corporateActions.Add(new StockDividendEvent(exDate, paymentDate, amount));
                        }
                    }
                }
            }
            var quoteRootObj = priceRootObj["quote"]![0]!;
            if (timeRootObj == null)
            {
                _log.Info($"Ticker {ticker} has no market price data at all.");
                return null;
            }
            var localStartTimes = timeRootObj!.AsArray().Select(n => n.GetLocalUnixDateTime()).ToArray();
            var highs = ToDecimalValues(quoteRootObj, "high");
            var lows = ToDecimalValues(quoteRootObj, "low");
            var opens = ToDecimalValues(quoteRootObj, "open");
            var closes = ToDecimalValues(quoteRootObj, "close");
            var volumes = ToDecimalValues(quoteRootObj, "volume");

            static decimal[]? ToDecimalValues(JsonNode? parentNode, string key)
            {
                return parentNode?[key]?.AsArray().Select(n => n.GetDecimal()).ToArray();
            }

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
            return new PricesAndCorporateActions(prices, corporateActions);
        }
        catch (Exception ex)
        {
            _log.Error($"Error when downloading prices for {ticker}.", ex);
            return null;
        }
    }
}
