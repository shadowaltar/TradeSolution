using Autofac;
using Autofac.Core;
using Common;
using Iced.Intel;
using log4net;
using log4net.Config;
using Microsoft.CodeAnalysis.Text;
using Microsoft.Diagnostics.Symbols;
using OfficeOpenXml;
using System;
using System.Diagnostics;
using System.Text;
using TradeCommon.Calculations;
using TradeCommon.Constants;
using TradeCommon.Essentials;
using TradeCommon.Essentials.Accounts;
using TradeCommon.Essentials.Instruments;
using TradeCommon.Essentials.Quotes;
using TradeCommon.Essentials.Trading;
using TradeCommon.Reporting;
using TradeCommon.Runtime;
using TradeDataCore.Instruments;
using TradeDataCore.MarketData;
using TradeLogicCore;
using TradeLogicCore.Algorithms;
using TradeLogicCore.Algorithms.Parameters;
using TradeLogicCore.Algorithms.Screening;
using TradeLogicCore.Services;
using Dependencies = TradeLogicCore.Dependencies;

public class Program
{
    private static readonly ILog _log = Logger.New();

    private static readonly int _maxPrintCount = 10;

    private static readonly string _testUserName = "test";
    private static readonly string _testPassword = "testtest";
    private static readonly string _testAccountName = "0";
    private static readonly BrokerType _broker = BrokerType.Binance;
    private static readonly ExchangeType _exchange = ExchangeType.Binance;
    private static readonly EnvironmentType _environment = EnvironmentType.Test;

    public static async Task Main(string[] args)
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        XmlConfigurator.Configure();
        ExcelPackage.LicenseContext = LicenseContext.NonCommercial;

        await RunMacMimicWebService();
        //await NewOrderDemo();
        //await RunRumiBackTestDemo();
        //await RunMACBackTestDemo();
        //await Run();

        Console.WriteLine("Finished.");
    }

    private static async Task RunMacMimicWebService()
    {
        // mimic set env + login
        var exchange = ExchangeType.Binance;
        var environment = EnvironmentType.Uat;
        var userName = "test";
        var accountName = "spot";
        var symbol = "BTCBUSD";
        var secType = SecurityType.Fx;
        var interval = IntervalType.OneMinute;
        var fastMa = 3;
        var slowMa = 7;
        var stopLoss = 0.0005m;
        var password = "testtest";

        Dependencies.Register(ExternalNames.Convert(exchange), exchange, environment);

        var adminService = Dependencies.ComponentContext.Resolve<IAdminService>();
        var securityService = Dependencies.ComponentContext.Resolve<ISecurityService>();

        var broker = ExternalNames.Convert(exchange);
        adminService.Initialize(environment, exchange, broker);

        //if (user == null) return BadRequest("Invalid user or credential.");
        var result = await adminService.Login(userName, password, accountName, adminService.Context.Environment);
        //if (result != ResultCode.LoginUserAndAccountOk) return BadRequest($"Failed to {nameof(SetEnvironmentAndLogin)}; code: {result}");

        var core = Dependencies.ComponentContext.Resolve<Core>();
        var security = await securityService.GetSecurity(symbol, core.Exchange, secType);
        //if (security == null) return BadRequest("Invalid or missing security.");

        AlgoEffectiveTimeRange algoTimeRange = null;
        switch (core.Environment)
        {
            //case EnvironmentType.Prod:
            //    algoTimeRange = AlgoEffectiveTimeRange.ForProduction(interval);
            //    break;
            //case EnvironmentType.Test:
            //    if (ControllerValidator.IsBadOrParse(startStr, out DateTime start, out br)) return br;
            //    if (ControllerValidator.IsBadOrParse(endStr, out DateTime end, out br)) return br;
            //    algoTimeRange = AlgoEffectiveTimeRange.ForBackTesting(start, end);
            //    break;
            case EnvironmentType.Uat:
                algoTimeRange = AlgoEffectiveTimeRange.ForPaperTrading(interval);
                break;
                //default:
                //    return BadRequest("Invalid environment.");
        }
        var parameters = new AlgoStartupParameters(adminService.CurrentUser.Name,
            adminService.CurrentAccount.Name, core.Environment, core.Exchange, core.Broker, interval,
            new List<Security> { security }, algoTimeRange);

        var algorithm = new MovingAverageCrossing(fastMa, slowMa, stopLoss) { Screening = new SingleSecurityLogic(security) };
        var guid = await core.StartAlgorithm(parameters, algorithm);

        Thread.Sleep(TimeSpan.FromMinutes(10));

        await core.StopAlgorithm(guid);
    }

    private static async Task Run()
    {
        Dependencies.Register(_broker, _exchange, _environment);

        var core = Dependencies.ComponentContext.Resolve<Core>();
        var services = Dependencies.ComponentContext.Resolve<IServices>();

        var account = await CheckTestUserAndAccount(services);
        var security = await services.Security.GetSecurity("ETHUSDT", _exchange, SecurityType.Fx);
        if (security == null)
        {
            _log.Error("Security is not found.");
            return;
        }
        var securityPool = new List<Security> { security };
        var algorithm = new MovingAverageCrossing(3, 7, 0.0005m) { Screening = new SingleSecurityLogic(security) };

        var parameters = new AlgoStartupParameters(_testUserName, account.Name, _environment, _exchange, _broker,
            IntervalType.OneMinute, securityPool, AlgoEffectiveTimeRange.ForBackTesting(new DateTime(2022, 1, 1), DateTime.UtcNow));

        var loginResult = await services.Admin.Login(parameters.UserName, _testPassword, parameters.AccountName, parameters.Environment);
        if (loginResult != ResultCode.LoginUserAndAccountOk) throw new InvalidOperationException(loginResult.ToString());

        await core.StartAlgorithm(parameters, algorithm);
    }

    private static async Task<Account> CheckTestUserAndAccount(IServices services)
    {
        var user = await services.Admin.GetUser(_testUserName, _environment);
        if (user == null)
        {
            var email = _testUserName + "@test.com";
            var count = await services.Admin.CreateUser(_testUserName, _testPassword, email, _environment);
            user = await services.Admin.GetUser(_testUserName, _environment);
            if (count == 0 || user == null)
            {
                _log.Error("Failed to create test user.");
                throw new InvalidOperationException();
            }
        }
        var account = await services.Admin.GetAccount(_testAccountName, _environment);
        if (account == null)
        {
            var count = await services.Admin.CreateAccount(new Account
            {
                Name = _testAccountName,
                OwnerId = user.Id,
                Type = "spot",
                SubType = "",
                BrokerId = ExternalNames.GetBrokerId(_broker),
                CreateTime = DateTime.UtcNow,
                UpdateTime = DateTime.UtcNow,
                Environment = _environment,
                ExternalAccount = _testAccountName,
                FeeStructure = "",
            });
            account = await services.Admin.GetAccount(_testAccountName, _environment);
            if (count == 0 || account == null)
            {
                _log.Error("Failed to create test account.");
                throw new InvalidOperationException();
            }
        }
        return account;
    }

    private static async Task NewSecurityOhlcSubscriptionDemo()
    {
        var securityService = Dependencies.ComponentContext.Resolve<ISecurityService>();
        var security = await securityService.GetSecurity("BTCTUSD", ExchangeType.Binance, SecurityType.Fx);
        if (security == null) return;
        var printCount = 0;
        var dataService = Dependencies.ComponentContext.Resolve<IMarketDataService>();
        var interval = IntervalType.OneMinute;
        dataService.NextOhlc += OnNewOhlc;
        await dataService.SubscribeOhlc(security, interval);
        while (printCount < _maxPrintCount)
        {
            Thread.Sleep(100);
        }
        await dataService.UnsubscribeOhlc(security, interval);

        void OnNewOhlc(int securityId, OhlcPrice price)
        {
            printCount++;
            if (security != null && security.Id != securityId)
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

        var adminService = Dependencies.ComponentContext.Resolve<IAdminService>();
        var portfolioService = Dependencies.ComponentContext.Resolve<IPortfolioService>();
        var orderService = Dependencies.ComponentContext.Resolve<IOrderService>();
        var tradeService = Dependencies.ComponentContext.Resolve<ITradeService>();
        var securityService = Dependencies.ComponentContext.Resolve<ISecurityService>();

        orderService.AfterOrderSent += AfterOrderSent;
        orderService.OrderCancelled += OnOrderCancelled;
        tradeService.NextTrades += OnNewTradesReceived;

        var resultCode = await adminService.Login(_testUserName, _testPassword, _testAccountName, _environment);
        if (resultCode != ResultCode.LoginUserAndAccountOk) throw new InvalidOperationException("Login failed with code: " + resultCode);

        orderService.CancelAllOpenOrders();
        var security = await securityService.GetSecurity("BTCTUSD", ExchangeType.Binance, SecurityType.Fx);
        if (security == null)
            return;

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
            await adminService.GetAccount(_testAccountName, _environment, false);
            orderService.SendOrder(order, false);
        });

        while (true)
        {
            Thread.Sleep(100);
        }

        async void AfterOrderSent(Order order)
        {
            _log.Info("Order sent: " + order);
            var orders = await orderService.GetOpenOrders();

            foreach (var openOrder in orders)
            {
                _log.Info("Existing Open Order: " + openOrder);
            }

            orderService.CancelOrder(order.Id);

            orderService.CancelAllOpenOrders();
        }

        static void OnNewTradesReceived(Trade[] trades)
        {
            foreach (var trade in trades)
            {
                _log.Info("Trade received: " + trade);
            }
        }
        static void OnOrderCancelled(Order order) => _log.Info("Order cancelled: " + order);
    }

    private static async Task RunRumiBackTestDemo()
    {
        var services = Dependencies.ComponentContext.Resolve<IServices>();
        var securityService = services.Security;

        //var filter = "00001,00002,00005";

        var securities = await securityService.GetSecurities(SecurityType.Fx, ExchangeType.Binance);

        securities = securities.Where(s => s.Code is "BTCUSDT").ToList();

        var stopLosses = new List<decimal> { 0.015m };
        var intervalTypes = new List<IntervalType> { IntervalType.OneMinute };

        var summaryRows = new List<List<object>>();

        var fast = 26;
        var slow = 51;
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
                    var securityPool = new List<Security> { security };
                    var algorithm = new Rumi(fast, slow, rumi, sl) { Screening = new SingleSecurityLogic(security) };
                    var engine = new AlgorithmEngine<RumiVariables>(services, algorithm);
                    var timeRange = new AlgoEffectiveTimeRange { DesignatedStart = start, DesignatedStop = end };
                    var algoStartParams = new AlgoStartupParameters(_testUserName, _testAccountName,
                        _environment, _exchange, _broker, interval, securityPool, timeRange);
                    await engine.Run(algoStartParams);

                    var entries = engine.GetAllEntries(security.Id);
                    if (entries.IsNullOrEmpty())
                        continue;

                    if (engine.Portfolio.FreeCash == engine.Portfolio.InitialFreeCash)
                    {
                        _log.Info($"No trades at all: {security.Code} {security.Name}");
                        continue;
                    }
                    var annualizedReturn = Metrics.GetAnnualizedReturn(engine.InitialFreeAmount.ToDouble(), engine.Portfolio.Notional.ToDouble(), start, end);
                    if (annualizedReturn == 0)
                    {
                        _log.Info($"No trades at all: {security.Code}");
                        continue;
                    }

                    var intervalStr = IntervalTypeConverter.ToIntervalString(interval);
                    var filePath = Path.Combine(@"C:\Temp", subFolder, $"{security.Code}-{intervalStr}.csv");
                    var tradeCount = entries.Count(e => e.LongCloseType != CloseType.None);
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
                        entries.Count(e => e.LongCloseType == CloseType.StopLoss),
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
        List<string> headers = new List<string> {
            "StartAlgorithm",
            "End",
            "Interval",
            "StopLoss",
            "Code",
            "Name",
            "EndFreeCash",
            "Stdev(All)",
            "AnnualizedReturn",
            "TradeCount",
            "SL Cnt",
            "PNL>0 Cnt",
            "PNL>0/Total Cnt",
            "FilePath"
        };
        var services = Dependencies.ComponentContext.Resolve<IServices>();
        var securityService = services.Security;

        var filter = "ETHUSDT";
        //var filter = "ETHUSDT";
        var filterCodes = filter.Split(',', StringSplitOptions.RemoveEmptyEntries).ToList();

        var securities = await securityService.GetSecurities(SecurityType.Fx, ExchangeType.Binance);
        securities = securities!.Where(s => filterCodes.ContainsIgnoreCase(s.Code)).ToList();

        var fast = 26;
        var slow = 51;
        var stopLosses = new List<decimal> { 0.0025m, 0.005m, 0.0075m };
        var intervalTypes = new List<IntervalType> { IntervalType.OneHour };
        //var fast = 2;
        //var slow = 5;
        //var stopLosses = new List<decimal> { 0.0002m, 0.00015m };
        //var intervalTypes = new List<IntervalType> { IntervalType.OneMinute };

        var summaryRows = new List<List<object>>();

        var periodTuples = new List<(DateTime start, DateTime end)>
        {
            (new DateTime(2020, 1, 1), new DateTime(2021, 1, 1)),
            (new DateTime(2021, 1, 1), new DateTime(2022, 1, 1)),
            (new DateTime(2022, 1, 1), new DateTime(2023, 1, 1)),
            (new DateTime(2023, 1, 1), new DateTime(2023, 7, 1)),
            (new DateTime(2020, 1, 1), new DateTime(2023, 7, 1)),
        };
        var now = DateTime.Now;

        var rootFolder = @"C:\Temp";

        var columns = ColumnMappingReader.Read(typeof(AlgoEntry<MacVariables>));

        var folder = Path.Combine(rootFolder, $"MAC-{fast},{slow},{now:yyyyMMdd-HHmmss}");
        var zipFilePath = Path.Combine(folder, $"Result-MAC-{fast},{slow},{now:yyyyMMdd-HHmmss}.zip");
        var summaryFilePath = Path.Combine(folder, $"!Summary-{fast},{slow},{now:yyyyMMdd-HHmmss}.csv");
        if (!Directory.Exists(folder))
            Directory.CreateDirectory(folder);

        await Parallel.ForEachAsync(securities, async (security, t) =>
        {
            foreach ((DateTime start, DateTime end) in periodTuples)
            {
                foreach (var interval in intervalTypes)
                {
                    var intervalStr = IntervalTypeConverter.ToIntervalString(interval);
                    foreach (var sl in stopLosses)
                    {

                        var initCash = 1000;


                        var securityPool = new List<Security> { security };
                        var algo = new MovingAverageCrossing(fast, slow, sl) { Screening = new SingleSecurityLogic(security) };
                        var engine = new AlgorithmEngine<MacVariables>(services, algo);
                        var timeRange = new AlgoEffectiveTimeRange { DesignatedStart = start, DesignatedStop = end };
                        var algoStartParams = new AlgoStartupParameters(_testUserName, _testAccountName,
                            _environment, _exchange, _broker, interval, securityPool, timeRange);

                        await engine.Run(algoStartParams);

                        var entries = engine.GetAllEntries(security.Id);
                        if (entries.IsNullOrEmpty())
                            continue;

                        if (engine.Portfolio.FreeCash == engine.Portfolio.InitialFreeCash)
                        {
                            _log.Info($"No trades at all: {security.Code} {security.Name}");
                            continue;
                        }
                        var endNotional = engine.Portfolio.Notional.ToDouble();
                        var annualizedReturn = Metrics.GetAnnualizedReturn(initCash, endNotional, start, end);
                        if (annualizedReturn == 0)
                        {
                            _log.Info($"No trades at all: {security.Code}");
                            continue;
                        }
                        // write detail file
                        var detailFilePath = Path.Combine(folder, $"{security.Code}-{start:yyyyMMdd}-{end:yyyyMMdd}-{endNotional}-{intervalStr}.csv");
                        Csv.Write(columns, entries, detailFilePath);

                        var tradeCount = entries.Count(e => e.LongCloseType != CloseType.None);
                        var positiveCount = entries.Where(e => e.RealizedPnl > 0).Count();
                        var summary = new List<object>
                        {
                            start,
                            end,
                            intervalStr,
                            sl,
                            security.Code,
                            security.Name,
                            engine.Portfolio.FreeCash,
                            Metrics.GetStandardDeviation(entries.Select(e => e.Return.ToDouble()).ToList()),
                            annualizedReturn.ToString("P4"),
                            tradeCount,
                            entries.Count(e => e.LongCloseType == CloseType.StopLoss),
                            positiveCount,
                            positiveCount / (double)tradeCount,
                            detailFilePath,
                        };
                        _log.Info($"Result: {string.Join('|', summary)}");
                        lock (summaryRows)
                            summaryRows.Add(summary);

                        _log.Info("----------------");
                        _log.Info(Printer.Print(headers));
                        _log.Info(Printer.Print(summaryRows));
                        _log.Info("----------------");
                    }
                }
            }
        });

        // write summary file
        summaryRows = summaryRows.OrderBy(r => r[0]).ToList();

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
    }
}