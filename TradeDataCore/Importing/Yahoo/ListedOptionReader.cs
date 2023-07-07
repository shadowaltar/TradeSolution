using log4net;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using TradeDataCore.Essentials;
using TradeDataCore.StaticData;
using TradeDataCore.Utils;

namespace TradeDataCore.Importing.Yahoo
{
    public class ListedOptionReader
    {
        private static readonly ILog _log = LogManager.GetLogger(typeof(ListedOptionReader));

        public async Task<IDictionary<int, Dictionary<SecurityStatType, decimal>>> ReadUnderlyingStats(IEnumerable<Security> securities)
        {
            const string url = @"https://query1.finance.yahoo.com/v7/finance/options/{0}";

            var tickers = securities.ToDictionary(s => Identifiers.ToYahooSymbol(s.Code, s.Exchange), s => s);
            var results = new ConcurrentDictionary<int, Dictionary<SecurityStatType, decimal>>();
            using var httpClient = new HttpClient();

            var buckets = tickers.Split(200);
            ParallelOptions parallelOptions = new() { MaxDegreeOfParallelism = 50 };
            foreach (var bucket in buckets)
            {
                await Parallel.ForEachAsync(tickers, parallelOptions, async (tuple, ct) =>
                {
                    var ticker = tuple.Key;
                    var security = tuple.Value;
                    var actualUrl = string.Format(url, ticker);
                    string json;
                    try
                    {
                        json = await httpClient.GetStringAsync(actualUrl);

                        var jo = JsonNode.Parse(json)?.AsObject();
                        if (jo == null)
                            return;

                        var rootObj = jo["optionChain"]!["result"]?.AsArray()[0]?["quote"];
                        if (rootObj == null) return;

                        var map = new Dictionary<SecurityStatType, decimal>
                        {
                            [SecurityStatType.MarketCap] = rootObj["marketCap"].GetDecimal()
                        };
                        results[security.Id] = map;
                    }
                    catch (HttpRequestException e)
                    {
                        _log.Error($"Yahoo options does not exist for ticker {ticker}. StatusCode: {e.StatusCode}. Message: {e.Message}. Url: {actualUrl}", e);
                    }
                    catch (Exception e)
                    {
                        _log.Error($"Yahoo options does not exist for ticker {ticker}. Url: {url}", e);
                    }
                });
            }
            return results;
        }
    }
}
