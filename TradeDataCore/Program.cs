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
var securities = await Storage.ReadSecurities(exchange, SecurityType.Equity);
var idTable = await Storage.Execute("SELECT DISTINCT SecurityId FROM " + DatabaseNames.StockPrice1hTable, DatabaseNames.MarketData);

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

var extendedResults = allPrices.SelectMany(tuple => tuple.Value)
    .OrderBy(i => i.Exchange).ThenBy(i => i.Code).ThenBy(i => i.Interval).ThenBy(i => i.Start)
    .ToList();
var filePath = await JsonWriter.ToJsonFile(extendedResults);