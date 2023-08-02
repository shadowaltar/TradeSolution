using Common;
using log4net;
using System.Text.Json.Nodes;
using TradeCommon.Constants;
using TradeCommon.Essentials;
using TradeCommon.Essentials.Corporates;
using TradeCommon.Essentials.Fundamentals;
using TradeCommon.Essentials.Instruments;
using TradeCommon.Essentials.Prices;
using TradeCommon.Essentials.Quotes;
using TradeCommon.Database;
using TradeDataCore.Essentials;
using System.Collections.Generic;

namespace TradeDataCore.Importing.Yahoo;

public class HistoricalPriceReader : IHistoricalPriceReader
{
    private static readonly ILog _log = Logger.New();

    public async Task<Dictionary<int, List<OhlcPrice>>?> ReadPrices(List<Security> securities, DateTime start, DateTime end, IntervalType intervalType)
    {
        IDictionary<int, PricesAndCorporateActions> results = await ReadYahooPrices(securities, intervalType, start, end);
        return results.ToDictionary(r => r.Key, r => r.Value.Prices);
    }

    /// <summary>
    /// String key here is the yahoo symbol. Format usually look like "xxxxxx.SZ" (or "SS") for SZ/SH exchanges, or "xxxx.HK" for HKEX.
    /// </summary>
    /// <param name="asOfTime"></param>
    /// <param name="tickers"></param>
    /// <returns></returns>
    public async Task<IDictionary<int, PricesAndCorporateActions>> ReadYahooPrices(List<Security> securities, IntervalType interval,
         DateTime startTime, DateTime endTime, params (FinancialStatType type, decimal value)[] filters)
    {
        return await ReadYahooPrices(securities, interval, startTime, endTime, TimeRangeType.Unknown, filters);
    }

    /// <summary>
    /// String key here is the yahoo symbol. Format usually look like "xxxxxx.SZ" (or "SS") for SZ/SH exchanges, or "xxxx.HK" for HKEX.
    /// </summary>
    /// <param name="asOfTime"></param>
    /// <param name="tickers"></param>
    /// <returns></returns>
    public async Task<IDictionary<int, PricesAndCorporateActions>> ReadYahooPrices(List<Security> securities, IntervalType interval,
         TimeRangeType range, params (FinancialStatType type, decimal value)[] filters)
    {
        return await ReadYahooPrices(securities, interval, null, null, range, filters);
    }

    private static async Task<IDictionary<int, PricesAndCorporateActions>> ReadYahooPrices(List<Security> securities, IntervalType interval,
        DateTime? start, DateTime? end, TimeRangeType range, params (FinancialStatType type, decimal value)[] filters)
    {
        var results = new Dictionary<int, PricesAndCorporateActions>();
        if (range == TimeRangeType.Unknown && (start == null || end == null))
        {
            throw new InvalidOperationException("Range must be set, or provide both start and end time.");
        }

        // filter out some unwanted securities.        
        if (filters.Length > 0)
        {
            securities = await FilterSecuritiesAsync(securities, filters);
        }

        using var httpClient = new HttpClient();

        var buckets = securities.Split(200).ToList();
        ParallelOptions parallelOptions = new() { MaxDegreeOfParallelism = 50 };
        for (int i = 0; i < buckets.Count; i++)
        {
            var bucket = buckets[i]!;
            _log.Info($"Retrieving {i}/{buckets.Count} bucket.");
            await Parallel.ForEachAsync(bucket, parallelOptions, async (security, ct) =>
            {
                if (security.YahooTicker.IsBlank()) return;

                var ticker = security.YahooTicker;
                var formattedTicker = Uri.EscapeDataString(security.YahooTicker);
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
                    lock (results)
                        results[security.Id] = tuple!;
                }
            });
            Thread.Sleep(1000);
        }
        return results;
    }

    private static async Task<List<Security>> FilterSecuritiesAsync(List<Security> securities, (FinancialStatType type, decimal value)[] filters)
    {
        var allStats = await Storage.ReadFinancialStats();
        var filteredSecurities = new List<Security>();
        foreach (var (type, value) in filters)
        {
            if (type == FinancialStatType.MarketCap)
            {
                var stats = allStats.ToDictionary(k => k.SecurityId, v => v.MarketCap);
                foreach (var security in securities)
                {
                    if (stats.TryGetValue(security.Id, out var marketCap))
                    {
                        if (marketCap >= value)
                        {
                            filteredSecurities.Add(security);
                        }
                    }
                }
            }
        }
        _log.Info($"Security filter is applied. Before {securities.Count}, after {filteredSecurities.Count}.");
        return filteredSecurities;
    }

    private static async Task<PricesAndCorporateActions?> InternalReadYahooPrices(HttpClient httpClient,
        string ticker, IntervalType interval, string url)
    {
        var jo = await HttpHelper.ReadJson(url, httpClient, _log);
        if (jo == null)
            return null;

        var intervalTimeSpan = IntervalTypeConverter.ToTimeSpan(interval);
        try
        {
            var rootObj = jo["chart"]!["result"]!.AsArray()[0]!;
            var priceRootObj = rootObj["indicators"]!;
            var timeRootObj = rootObj["timestamp"]!;
            var corporateActions = new List<IStockCorporateAction>();

            decimal[]? adjustedCloses = null;
            if (interval == IntervalType.OneDay)
            {
                var adjustedClosesObj = priceRootObj!["adjclose"]?.AsArray()?[0]?["adjclose"];
                if (adjustedClosesObj != null)
                {
                    adjustedCloses = adjustedClosesObj!.AsArray().Select(n => (decimal)(n ?? 0m).AsValue()).ToArray();
                }

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
                            var exDate = contentObj.GetLocalFromUnixMs("date");
                            if (payableDate == default || numerator == default || denominator == default || exDate == default)
                            {
                                _log.Warn($"Invalid stock split entry. Payable:{payableDate.ToString(Constants.DefaultDateFormat)},Ex:{exDate.ToString(Constants.DefaultDateFormat)},N/D:{numerator}/{denominator}");
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
                            var paymentDate = contentObj.GetLocalFromUnixMs("date");
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

            var missingDataIndexes = new HashSet<int>(); // some items will have missing values
            var localStartTimes = timeRootObj!.AsArray().Select(n => n.GetLocalFromUnixMs()).ToArray();
            var highs = ToDecimalValues(quoteRootObj, "high");
            var lows = ToDecimalValues(quoteRootObj, "low");
            var opens = ToDecimalValues(quoteRootObj, "open");
            var closes = ToDecimalValues(quoteRootObj, "close");
            var volumes = ToDecimalValues(quoteRootObj, "volume");

            // parse the price element node into array of numbers
            decimal[]? ToDecimalValues(JsonNode? parentNode, string key)
            {
                var array = parentNode?[key]?.AsArray();
                if (array == null)
                {
                    _log.Error("Missing data! Data is: " + key);
                    return null;
                }
                var result = new decimal[array.Count];
                for (int i = 0; i < array.Count; i++)
                {
                    JsonNode? item = array[i];
                    if (item == null)
                    {
                        missingDataIndexes!.Add(i);
                    }
                    else
                    {
                        var x = item.GetValue<decimal>();
                        result[i] = x;
                    }
                }
                return result;
            }

            if (opens == null || highs == null || lows == null || closes == null || volumes == null)
            {
                _log.Error("Cannot construct OHLC items: some price elements are missing.");
                return null;
            }

            // create the OHLC entries.
            var prices = new List<OhlcPrice>();
            // the last bar from yahoo is always the real-time bar; ignore it since it is most like not aligned with 1m/1h/1d...
            for (var i = 0; i < localStartTimes.Length; i++)
            {
                if (missingDataIndexes.Contains(i))
                {
                    continue;
                }

                if (i == localStartTimes.Length - 1)
                {
                    var last2Time = localStartTimes[i - 1];
                    var last1Time = localStartTimes[i];
                    if (last1Time - last2Time != intervalTimeSpan)
                    {
                        continue;
                    }
                }
                // TODO hardcode assumption: prices only have 8 dp; vol has 8.
                opens[i] = Math.Round(opens[i], 8, MidpointRounding.ToEven);
                highs[i] = Math.Round(highs[i], 8, MidpointRounding.ToEven);
                lows[i] = Math.Round(lows[i], 8, MidpointRounding.ToEven);
                closes[i] = Math.Round(closes[i], 8, MidpointRounding.ToEven);
                volumes[i] = Math.Round(volumes[i], 8, MidpointRounding.ToEven);
                var adjustedClose = adjustedCloses != null ? Math.Round(adjustedCloses[i], 8, MidpointRounding.ToEven) : closes[i];

                var price = new OhlcPrice(opens[i], highs[i], lows[i], closes[i], adjustedClose, volumes[i], localStartTimes[i]);
                prices.Add(price);
            }

            // print the dates of missing data
            var missingDataTimes = new List<DateTime>(missingDataIndexes.Count);
            foreach (var index in missingDataIndexes.OrderBy(i => i))
            {
                missingDataTimes.Add(localStartTimes[index]);
            }
            if (missingDataTimes.Count > 0)
            {
                _log.Warn("Data entries with below start times are ignored as they don't have valid price elements: "
                    + string.Join(',', missingDataTimes.Select(t => t.ToString(Constants.DefaultDateTimeFormat))));
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
