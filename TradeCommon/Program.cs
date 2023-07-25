// See https://aka.ms/new-console-template for more information
using BenchmarkDotNet.Running;
using log4net.Config;
using OfficeOpenXml;
using System.Text;
using TradeCommon.CodeAnalysis;

Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
XmlConfigurator.Configure();
ExcelPackage.LicenseContext = LicenseContext.NonCommercial;

var summary = BenchmarkRunner.Run<BinaryFunctionsBenchmark>();