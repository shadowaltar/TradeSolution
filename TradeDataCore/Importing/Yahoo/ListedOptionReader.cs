﻿using Common;
using log4net;
using TradeCommon.Essentials.Fundamentals;
using TradeCommon.Essentials.Instruments;
using Security = TradeCommon.Essentials.Instruments.Security;

namespace TradeDataCore.Importing.Yahoo;

public class ListedOptionReader
{
    private static readonly ILog _log = Logger.New();

    public async Task<List<FinancialStat>> ReadUnderlyingStats(IEnumerable<Security> securities)
    {
        const string url = @"https://query1.finance.yahoo.com/v7/finance/options/{0}";

        var tickers = securities.Where(s => SecurityTypeConverter.IsEquity(s.Type))
            .ToDictionary(s => Identifiers.ToYahooSymbol(s.Code, s.Exchange), s => s);

        _log.Info($"Retrieving {tickers.Count} tickers financial stats from Yahoo.");
        var results = new List<FinancialStat>();
        using var httpClient = new HttpClient();

        var buckets = tickers.Split(200).ToList();
        ParallelOptions parallelOptions = new() { MaxDegreeOfParallelism = 50 };
        for (int i = 0; i < buckets.Count; i++)
        {
            List<KeyValuePair<string, Security>>? bucket = buckets[i];
            _log.Info($"Retrieving {i}/{buckets.Count} bucket.");
            await Parallel.ForEachAsync(tickers, parallelOptions, async (tuple, ct) =>
            {
                var ticker = tuple.Key;
                var security = tuple.Value;
                var actualUrl = string.Format(url, ticker);
                try
                {
                    var jo = await httpClient.ReadJson(actualUrl, _log);
                    if (jo == null)
                        return;

                    var rootObj = jo["optionChain"]?["result"]?.AsArray()[0]?["quote"];
                    if (rootObj == null)
                        return;

                    var stat = new FinancialStat
                    {
                        SecurityId = security.Id,
                        MarketCap = rootObj["marketCap"].GetDecimal(),
                    };
                    lock (results)
                        results.Add(stat);
                }
                catch (Exception e)
                {
                    _log.Error($"No financial stats data (from option chain) loaded for {ticker}; url: {actualUrl}");
                }
            });
        }
        return results;
    }
}
