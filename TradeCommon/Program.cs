// See https://aka.ms/new-console-template for more information
using BenchmarkDotNet.Running;
using log4net.Config;
using OfficeOpenXml;
using System.Text;
using TradeCommon.CodeAnalysis;
using TradeCommon.Database;
using TradeCommon.Utils.Common;

Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
XmlConfigurator.Configure();
ExcelPackage.LicenseContext = LicenseContext.NonCommercial;

//var hash = CryptographyUtils.HashString("abc", "special.trading.unicorn");
//Console.WriteLine(hash);
//var summary = BenchmarkRunner.Run<BinaryFunctionsBenchmark>();
//var summary = BenchmarkRunner.Run<ExternalExecutionStateBenchmark>();