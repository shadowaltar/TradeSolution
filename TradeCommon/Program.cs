// See https://aka.ms/new-console-template for more information
using BenchmarkDotNet.Running;
using log4net;
using log4net.Config;
using OfficeOpenXml;
using System.Text;
using TradeCommon.CodeAnalysis;
using TradeCommon.Constants;
using TradeCommon.Essentials.Instruments;
using TradeCommon.Importing;
using TradeDataCore.Importing;

Console.WriteLine("Hello, World!");

Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
XmlConfigurator.Configure();
ExcelPackage.LicenseContext = LicenseContext.NonCommercial;


var summary = BenchmarkRunner.Run<PoolBenchmark>();