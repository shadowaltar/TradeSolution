using log4net.Config;
using OfficeOpenXml;
using System.Diagnostics;
using System.Text;
using TradeDataCore.Database;
using TradeDataCore.Essentials;
using TradeDataCore.StaticData;

Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
XmlConfigurator.Configure();
ExcelPackage.LicenseContext = LicenseContext.NonCommercial;

//await Storage.CreateSecurityTable();
//await Storage.CreatePriceTable();
//var importer = new SecurityDefinitionImporter();
//var securities = await importer.DownloadAndParseHongKongSecurityDefinitions();

var securities = await Storage.ReadSecurities("HKEX");
var tickers = new Dictionary<string, int>();
foreach (var security in securities.Take(2))
{
    tickers[Identifiers.ToYahooSymbol(security.Code, security.Exchange)] = security.Id;
}

var interval = IntervalType.OneDay;
var priceReader = new TradeDataCore.Importing.Yahoo.PriceReader();
var allPrices = await priceReader.ReadYahooPrices(tickers.Select(p => p.Key).ToList(),
    interval, TimeRangeType.YearToDay);

var intervalStr = IntervalTypeConverter.ToIntervalString(interval);
foreach (var (ticker, id) in tickers)
{
    if (allPrices.TryGetValue(ticker, out var prices))
    {
        await Storage.InsertPrices(id, intervalStr, prices);
        var count = await Storage.Execute("SELECT COUNT(Interval) FROM " + DatabaseNames.PriceTable, DatabaseNames.MarketData);
        Debug.WriteLine(count);
    }
}
foreach (var (ticker, id) in tickers)
{
    var prices = await Storage.ReadPrices(id, intervalStr, new DateTime(2005, 1, 1));
    Debug.WriteLine(prices.Count);
}
