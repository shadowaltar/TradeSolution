﻿using Common;
using log4net;
using TradeCommon.Essentials;
using TradeCommon.Essentials.Instruments;
using TradeCommon.Essentials.Quotes;
using TradeCommon.Externals;
using TradeCommon.Utils.Common;

namespace TradeConnectivity.Binance.Services;
public class HistoricalMarketData : IExternalHistoricalMarketDataManagement
{
    private static readonly ILog _log = Logger.New();

    public async Task<Dictionary<int, List<OhlcPrice>>?> ReadPrices(List<Security> securities, DateTime start, DateTime end, IntervalType intervalType)
    {
        var results = new Dictionary<int, List<OhlcPrice>>();
        await Parallel.ForEachAsync(securities, async (security, ct) =>
        {
            var code = security.Code;
            var prices = await ReadPrices(code, start, end, intervalType);
            if (prices == null)
                return;
            lock (results)
                results[security.Id] = prices;
        });
        return results;
    }

    public async Task<List<OhlcPrice>?> ReadPrices(string code, DateTime start, DateTime end, IntervalType intervalType)
    {
        static string UpdateTimeFrame(string c, string i, long s, long e) =>
            $"{RootUrls.DefaultHttps}/klines?symbol={c}&interval={i}&startTime={s}&endTime={e}";

        var intervalStr = IntervalTypeConverter.ToIntervalString(intervalType);
        if (end > DateTime.UtcNow)
        {
            end = DateTime.UtcNow;
        }
        var startMs = start.ToUnixMs();
        var endMs = end.ToUnixMs();

        string url = UpdateTimeFrame(code, intervalStr, startMs, endMs);
        using var httpClient = new HttpClient();

        var prices = new List<OhlcPrice>();
        long lastEndMs = 0l;
        while (lastEndMs < endMs)
        {
            var jo = await HttpHelper.ReadJsonArray(url, httpClient, _log);
            if (jo == null || jo.Count == 0)
                break;

            foreach (var item in jo)
            {
                var array = item?.AsArray();
                if (array == null)
                    continue;
                var barStartMs = array[0]!.GetValue<long>();
                var open = array[1]!.GetValue<string>().ParseDecimal();
                var high = array[2]!.GetValue<string>().ParseDecimal();
                var low = array[3]!.GetValue<string>().ParseDecimal();
                var close = array[4]!.GetValue<string>().ParseDecimal();
                var volume = array[5]!.GetValue<string>().ParseDecimal();
                var barEndMs = array[6]!.GetValue<long>();

                lastEndMs = barEndMs; // binance always set candle's end time one ms smaller than next start time
                prices.Add(new OhlcPrice(open, high, low, close, volume, DateUtils.FromUnixMs(barStartMs)));
            }

            startMs = lastEndMs + 1;
            url = UpdateTimeFrame(code, intervalStr, startMs, endMs);
        }

        return prices;
    }
}
