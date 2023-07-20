// See https://aka.ms/new-console-template for more information
using log4net;
using log4net.Config;
using OfficeOpenXml;
using System.Text;
using TradeCommon.Constants;
using TradeCommon.Essentials.Instruments;
using TradeCommon.Importing;
using TradeDataCore.Importing;

Console.WriteLine("Hello, World!");

Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
XmlConfigurator.Configure();
ExcelPackage.LicenseContext = LicenseContext.NonCommercial;

var reader = new ExcelReader();
var securities = reader.ReadSheet<Security>("C:\\Temp\\A.xlsx", "StaticData.HKSecurityExcelDefinition",
    new ExcelImportSetting
    {
        HeaderSkipLineCount = 2,
        HardcodedValues = new()
        {
                        { nameof(Security.Exchange), ExchangeNames.Hkex },
                        { nameof(Security.Currency), ForexNames.Hkd },
                        { nameof(Security.YahooTicker), new ComplexMapping(code => Identifiers.ToYahooSymbol((string)code, ExchangeNames.Hkex), nameof(Security.Code)) },
        }
    });