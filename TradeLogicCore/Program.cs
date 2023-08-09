﻿using Autofac;
using Common;
using log4net;
using log4net.Config;
using OfficeOpenXml;
using System.Diagnostics;
using System.Text;
using TradeCommon.Calculations;
using TradeCommon.Constants;
using TradeCommon.Essentials;
using TradeCommon.Essentials.Instruments;
using TradeCommon.Essentials.Quotes;
using TradeCommon.Essentials.Trading;
using TradeDataCore.Instruments;
using TradeDataCore.MarketData;
using TradeLogicCore;
using TradeLogicCore.Algorithms;
using TradeLogicCore.Services;
using Dependencies = TradeLogicCore.Dependencies;

public class Program
{
    private static readonly ILog _log = Logger.New();

    private static readonly int _maxPrintCount = 10;

    private static async Task Main(string[] args)
    {
        ILog log = Logger.New();

        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        XmlConfigurator.Configure();
        ExcelPackage.LicenseContext = LicenseContext.NonCommercial;

        Dependencies.Register(ExternalNames.Binance);

        await NewOrderDemo();
        //await RunMACBackTestDemo();

        var engine = Dependencies.ComponentContext.Resolve<Core>();
        await engine.Start("0");

        Console.WriteLine("Finished.");
    }

    private static async Task NewSecurityOhlcSubscriptionDemo()
    {
        var securityService = Dependencies.ComponentContext.Resolve<ISecurityService>();
        var security = await securityService.GetSecurity("BTCTUSD", ExchangeType.Binance, SecurityType.Fx);

        var printCount = 0;
        var dataService = Dependencies.ComponentContext.Resolve<IRealTimeMarketDataService>();
        dataService.NextOhlc += OnNewOhlc;
        await dataService.SubscribeOhlc(security);
        while (printCount < _maxPrintCount)
        {
            Thread.Sleep(100);
        }
        await dataService.UnsubscribeOhlc(security);

        void OnNewOhlc(int securityId, OhlcPrice price)
        {
            printCount++;
            if (security.Id != securityId)
            {
                Console.WriteLine("Impossible!");
                return;
            }
            Console.WriteLine(price);
        }
    }

    private static async Task NewOrderDemo()
    {
        var engine = Dependencies.ComponentContext.Resolve<Core>();

        var portfolioService = Dependencies.ComponentContext.Resolve<IPortfolioService>();
        var orderService = Dependencies.ComponentContext.Resolve<IOrderService>();
        var tradeService = Dependencies.ComponentContext.Resolve<ITradeService>();
        var securityService = Dependencies.ComponentContext.Resolve<ISecurityService>();

        orderService.AfterOrderSent += AfterOrderSent;
        orderService.OrderCancelled += OnOrderCancelled;
        tradeService.NextTrades += OnNewTradesReceived;

        portfolioService.SelectUser(new User());

        var security = await securityService.GetSecurity("BTCTUSD", ExchangeType.Binance, SecurityType.Fx);
        var order = new Order
        {
            SecurityCode = "BTCUSDT",
            SecurityId = security.Id,
            Price = 10000,
            Quantity = 0.01m,
            Side = Side.Buy,
            Type = OrderType.Limit,
            TimeInForce = OrderTimeInForceType.GoodTillCancel,
        };
        await Task.Run(async () =>
        {
            await portfolioService.GetAccountByName("whatever");
            orderService.SendOrder(order, false);
        });

        while (true)
        {
            Thread.Sleep(100);
        }

        async void AfterOrderSent(Order order)
        {
            Console.WriteLine(order);
            var orders = await orderService.GetOpenOrders();

            orderService.CancelAllOrders();

            orderService.CancelOrder(order.Id);
        }

        static void OnNewTradesReceived(List<Trade> trades)
        {
            foreach (var trade in trades)
            {
                Console.WriteLine(trade);
            }
        }
    }

    private static async Task RunRumiBackTestDemo()
    {
        var securityService = Dependencies.ComponentContext.Resolve<ISecurityService>();
        var mds = Dependencies.ComponentContext.Resolve<IHistoricalMarketDataService>();

        //var filter = "00001,00002,00005";

        var securities = await securityService.GetSecurities(ExchangeType.Binance, SecurityType.Fx);

        //securities = securities.Where(s => s.Code is "00001" or "00002" or "00005").ToList();

        var stopLosses = new List<decimal> { 0.05m };
        var intervalTypes = new List<IntervalType> { IntervalType.OneHour };

        var summaryRows = new List<List<object>>();

        var fast = 2;
        var slow = 5;
        var rumi = 1;
        var start = new DateTime(2022, 1, 1);
        var end = new DateTime(2023, 7, 1);
        var now = DateTime.Now;

        var rootFolder = @"C:\Temp";
        var subFolder = $"RUMI-{fast},{slow},{rumi}-{now:yyyyMMdd-HHmmss}";
        var zipFileName = $"Result-RUMI-{fast},{slow},{rumi}-{now:yyyyMMdd-HHmmss}.zip";
        var folder = Path.Combine(rootFolder, subFolder);
        var zipFilePath = Path.Combine(rootFolder, zipFileName);
        var summaryFilePath = Path.Combine(folder, $"!Summary-{fast},{slow},{rumi}-{now:yyyyMMdd-HHmmss}.csv");

        if (!Directory.Exists(folder))
            Directory.CreateDirectory(folder);

        await Parallel.ForEachAsync(securities, async (security, t) =>
        {
            foreach (var interval in intervalTypes)
            {
                foreach (var sl in stopLosses)
                {
                    security.PriceDecimalPoints = 6;
                    var engine = new AlgorithmEngine<RumiVariables>(mds);
                    var algo = new Rumi(engine, fast, slow, rumi, sl);
                    engine.SetAlgorithm(algo, algo.Sizing, algo.Entering, algo.Exiting, algo.Screening);

                    var initCash = 1000;
                    var entries = await engine.BackTest(new List<Security> { security }, interval, start, end, initCash);
                    if (entries.IsNullOrEmpty())
                        continue;

                    if (engine.Portfolio.FreeCash == engine.Portfolio.InitialFreeCash)
                    {
                        _log.Info($"No trades at all: {security.Code} {security.Name}");
                        continue;
                    }

                    var annualizedReturn = Metrics.GetAnnualizedReturn(initCash, engine.Portfolio.Notional.ToDouble(), start, end);
                    if (annualizedReturn == 0)
                    {
                        _log.Info($"No trades at all: {security.Code}");
                        continue;
                    }

                    var intervalStr = IntervalTypeConverter.ToIntervalString(interval);
                    var filePath = Path.Combine(@"C:\Temp", subFolder, $"{security.Code}-{intervalStr}.csv");
                    var tradeCount = entries.Count(e => e.IsClosing);
                    var positiveCount = entries.Where(e => e.RealizedPnl > 0).Count();
                    var result = new List<object>
                    {
                        security.Code,
                        security.Name,
                        intervalStr,
                        engine.Portfolio.FreeCash,
                        Metrics.GetStandardDeviation(entries.Select(e => e.Return.ToDouble()).ToList()),
                        annualizedReturn.ToString("P4"),
                        tradeCount,
                        entries.Count(e => e.IsStopLossTriggered),
                        positiveCount,
                        positiveCount / (double)tradeCount,
                        filePath,
                    };
                    Csv.Write(entries, filePath);

                    _log.Info($"Result: {string.Join('|', result)}");
                    lock (summaryRows)
                        summaryRows.Add(result);
                }
            }
        });

        summaryRows = summaryRows.OrderBy(r => r[0]).ToList();
        var headers = new List<string> {
            "SecurityCode",
            "SecurityName",
            "Interval",
            "EndFreeCash",
            "Stdev(All)",
            "AnnualizedReturn",
            "TradeCount",
            "SL Cnt",
            "+ve PNL Cnt",
            "+ve/Total Cnt",
            "FilePath"
        };

        Csv.Write(headers, summaryRows, summaryFilePath);
        Zip.Archive(folder, zipFilePath);

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = summaryFilePath,
                UseShellExecute = true,
            });
        }
        catch (Exception ex)
        {
            _log.Error($"Failed to open output file: {summaryFilePath}", ex);
        }

        _log.Info("RESULTS ----------");
        _log.Info(Printer.Print(summaryRows));
    }

    private static async Task RunMACBackTestDemo()
    {
        var securityService = Dependencies.ComponentContext.Resolve<ISecurityService>();
        var mds = Dependencies.ComponentContext.Resolve<IHistoricalMarketDataService>();

        var filter = "";
        var filterCodes = filter.Split(',', StringSplitOptions.RemoveEmptyEntries).ToList();

        var securities = await securityService.GetSecurities(ExchangeType.Hkex, SecurityType.Equity);
        //var securities = await securityService.GetSecurities(ExchangeType.Binance, SecurityType.Fx);
        securities = filterCodes.Count == 0 ? securities : securities.Where(s => filterCodes.ContainsIgnoreCase(s.Code)).ToList();

        var stopLosses = new List<decimal> { 0.05m };
        var intervalTypes = new List<IntervalType> { IntervalType.OneHour };

        var summaryRows = new List<List<object>>();

        var fast = 2;
        var slow = 5;
        var start = new DateTime(2022, 1, 1);
        var end = new DateTime(2023, 8, 1);
        var now = DateTime.Now;

        var rootFolder = @"C:\Temp";
        var subFolder = $"MAC-{fast},{slow}-{now:yyyyMMdd-HHmmss}";
        var zipFileName = $"Result-MAC-{fast},{slow}-{now:yyyyMMdd-HHmmss}.zip";
        var folder = Path.Combine(rootFolder, subFolder);
        var zipFilePath = Path.Combine(rootFolder, zipFileName);
        var summaryFilePath = Path.Combine(folder, $"!Summary-{fast},{slow}-{now:yyyyMMdd-HHmmss}.csv");

        if (!Directory.Exists(folder))
            Directory.CreateDirectory(folder);

        await Parallel.ForEachAsync(securities, async (security, t) =>
        {
            foreach (var interval in intervalTypes)
            {
                foreach (var sl in stopLosses)
                {
                    security.PriceDecimalPoints = 6;
                    var engine = new AlgorithmEngine<MacVariables>(mds);
                    var algo = new MovingAverageCrossing(engine, fast, slow, sl);
                    engine.SetAlgorithm(algo, algo.Sizing, algo.Entering, algo.Exiting, algo.Screening);

                    var initCash = 1000;
                    var entries = await engine.BackTest(new List<Security> { security }, interval, start, end, initCash);
                    if (entries.IsNullOrEmpty())
                        continue;

                    if (engine.Portfolio.FreeCash == engine.Portfolio.InitialFreeCash)
                    {
                        _log.Info($"No trades at all: {security.Code} {security.Name}");
                        continue;
                    }

                    var annualizedReturn = Metrics.GetAnnualizedReturn(initCash, engine.Portfolio.Notional.ToDouble(), start, end);
                    if (annualizedReturn == 0)
                    {
                        _log.Info($"No trades at all: {security.Code}");
                        continue;
                    }

                    var intervalStr = IntervalTypeConverter.ToIntervalString(interval);
                    var filePath = Path.Combine(@"C:\Temp", subFolder, $"{security.Code}-{intervalStr}.csv");
                    var tradeCount = entries.Count(e => e.IsClosing);
                    var positiveCount = entries.Where(e => e.RealizedPnl > 0).Count();
                    var result = new List<object>
                    {
                        security.Code,
                        security.Name,
                        intervalStr,
                        engine.Portfolio.FreeCash,
                        Metrics.GetStandardDeviation(entries.Select(e => e.Return.ToDouble()).ToList()),
                        annualizedReturn.ToString("P4"),
                        tradeCount,
                        entries.Count(e => e.IsStopLossTriggered),
                        positiveCount,
                        positiveCount / (double)tradeCount,
                        filePath,
                    };
                    Csv.Write(entries, filePath);

                    _log.Info($"Result: {string.Join('|', result)}");
                    lock (summaryRows)
                        summaryRows.Add(result);
                }
            }
        });

        summaryRows = summaryRows.OrderBy(r => r[0]).ToList();
        var headers = new List<string> {
            "SecurityCode",
            "SecurityName",
            "Interval",
            "EndFreeCash",
            "Stdev(All)",
            "AnnualizedReturn",
            "TradeCount",
            "SL Cnt",
            "+ve PNL Cnt",
            "+ve/Total Cnt",
            "FilePath"
        };

        Csv.Write(headers, summaryRows, summaryFilePath);
        Zip.Archive(folder, zipFilePath);

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = summaryFilePath,
                UseShellExecute = true,
            });
        }
        catch (Exception ex)
        {
            _log.Error($"Failed to open output file: {summaryFilePath}", ex);
        }

        _log.Info("RESULTS ----------");
        _log.Info(Printer.Print(summaryRows));
    }
}