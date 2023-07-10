using log4net.Config;
using OfficeOpenXml;
using System.Data;
using System.Text;
using TradeDataCore.Database;
using TradeDataCore.Essentials;
using TradeDataCore.Exporting;
using TradeDataCore.Utils;

Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
XmlConfigurator.Configure();
ExcelPackage.LicenseContext = LicenseContext.NonCommercial;

var exchange = "HKEX";
var intervalStr = "1d";
var rangeStr = "2y";
var securities = await Storage.ReadSecurities(exchange);
var idTable = await Storage.Execute("SELECT DISTINCT SecurityId FROM " + DatabaseNames.PriceTable, DatabaseNames.MarketData);

IEnumerable<int> GetSecIdFromTable()
{
    foreach (DataRow dr in idTable.Rows)
    {
        yield return dr["SecurityId"].ToString().ParseInt();
    }
}
var ids = GetSecIdFromTable().Distinct().ToList();
securities = securities.Where(s => ids.Contains(s.Id)).ToList();


var interval = IntervalTypeConverter.Parse(intervalStr);
var range = TimeRangeTypeConverter.Parse(rangeStr);
var start = TimeRangeTypeConverter.ConvertTimeSpan(range, OperatorType.Minus)(DateTime.Today);
var allPrices = await Storage.ReadAllPrices(securities, interval, range);

var secMap = securities.ToDictionary(s => s.Id, s => s);
var extendedResults = new List<ExtendedOhlcPrice>();
foreach (var (secId, prices) in allPrices)
{
    if (secMap.TryGetValue(secId, out var sec))
    {
        foreach (var p in prices)
        {
            extendedResults.Add(new ExtendedOhlcPrice(sec.Code, sec.Exchange, p.Open, p.High, p.Low, p.Close, p.Volume, intervalStr, start));
        }
    }
}
var filePath = await JsonWriter.ToJsonFile(extendedResults);