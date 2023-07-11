using log4net;
using TradeDataCore.Essentials;
using TradeDataCore.Utils;

namespace TradeDataCore.Importing.Binance
{
    public class HistoricalPriceReader
    {
        private static readonly ILog _log = LogManager.GetLogger(typeof(HistoricalPriceReader));

        public async Task<Dictionary<int, List<OhlcPrice>>?> ReadPrices(List<Security> securities, DateTime start, DateTime end, IntervalType intervalType)
        {
            var results = new Dictionary<int, List<OhlcPrice>>();
            foreach (var security in securities)
            {
                var code = security.Code;
                var prices = await ReadPrices(code, start, end, intervalType);
                if (prices == null)
                    continue;
                results[security.Id] = prices;
            }
            return results;
        }

        public async Task<List<OhlcPrice>?> ReadPrices(string code, DateTime start, DateTime end, IntervalType intervalType)
        {
            var intervalStr = IntervalTypeConverter.ToIntervalString(intervalType);
            var startMs = DateUtils.ToUnixMs(start);
            var endMs = DateUtils.ToUnixMs(end);
            string prodUrl = $"https://data-api.binance.vision/api/v3/klines?code={code}&interval={intervalStr}&startTime={startMs}&endTime={endMs}";

            using var httpClient = new HttpClient();

#if FAKE_RELEASE // a fake one
            using var reader = EmbeddedResourceReader.GetStreamReader("Importing.Binance.ExamplePrices", "json");
            if (reader == null)
                return null;

            var jo = JsonNode.Parse(reader.BaseStream)?.AsArray();
            if (jo == null)
                return null;
#endif

            if (end > DateTime.UtcNow)
                end = DateTime.UtcNow;

            var prices = new List<OhlcPrice>();
            var lastEndTime = DateTime.MinValue;
            while (lastEndTime < end)
            {
                var jo = await HttpHelper.ReadJsonArray(prodUrl, httpClient, _log);
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

                    lastEndTime = DateUtils.FromUnixMs(barEndMs + 1); // binance always set candle's end time one ms smaller than next start time
                    prices.Add(new OhlcPrice(open, high, low, close, volume, DateUtils.FromUnixMs(barStartMs)));
                }
            }

            return prices;
        }
    }
}
