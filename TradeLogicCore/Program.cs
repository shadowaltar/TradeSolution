using Autofac;
using Common;
using log4net;
using log4net.Config;
using OfficeOpenXml;
using System.Diagnostics;
using System.Text;
using TradeCommon.Algorithms;
using TradeCommon.Calculations;
using TradeCommon.Constants;
using TradeCommon.Database;
using TradeCommon.Essentials;
using TradeCommon.Essentials.Accounts;
using TradeCommon.Essentials.Algorithms;
using TradeCommon.Essentials.Instruments;
using TradeCommon.Essentials.Quotes;
using TradeCommon.Essentials.Trading;
using TradeCommon.Reporting;
using TradeCommon.Runtime;
using TradeDataCore.Instruments;
using TradeDataCore.MarketData;
using TradeLogicCore;
using TradeLogicCore.Algorithms;
using TradeLogicCore.Algorithms.Screening;
using TradeLogicCore.Services;
using Dependencies = TradeLogicCore.Dependencies;

public class Program
{
    private static readonly ILog _log = Logger.New();

    private static readonly int _maxPrintCount = 10;

    //private static readonly string _testUserName = "test";
    //private static readonly string _testPassword = "testtest";
    //private static readonly string _testEmail = "test@test.com";
    //private static readonly string _testAccountName = "test";
    //private static readonly string _testAccountType = "spot";
    //private static readonly EnvironmentType _testEnvironment = EnvironmentType.Test;

    private static readonly string _testUserName = "test";
    private static readonly string _testPassword = "testtest";
    private static readonly string _testEmail = "1688996631782681271@testnet.binance.vision";
    private static readonly string _testAccountName = "spot";
    private static readonly string _testAccountType = "spot";
    private static readonly DateTime _testStart = new DateTime(2022, 1, 1);
    private static readonly DateTime _testEnd = new DateTime(2023, 7, 1);

    private static readonly EnvironmentType _testEnvironment = EnvironmentType.Uat;

    private static readonly BrokerType _testBroker = BrokerType.Binance;
    private static readonly ExchangeType _testExchange = ExchangeType.Binance;

    private static string _fakeSecretFileContent;
    private static string _fakeSecretFilePath;

    public static async Task Main(string[] args)
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        XmlConfigurator.Configure();
        ExcelPackage.LicenseContext = LicenseContext.NonCommercial;

        //await ResetTables();
        await RunMacMimicWebService();
        //await NewOrderDemo();
        //await RunRumiBackTestDemo();
        //await RunMACBackTestDemo();
        //await Run();
    }

    private static async Task ResetTables()
    {
        //var storage = new Storage();
        //await storage.CreateTable<Order>("stock_orders");
        //await storage.CreateTable<Trade>("stock_trades");
        //await storage.CreateTable<Position>("stock_positions");
        //await storage.CreateTable<Order>("fx_orders");
        //await storage.CreateTable<Trade>("fx_trades");
        //await storage.CreateTable<Position>("fx_positions");
        //await storage.CreateTable<PositionRecord>();
        //await storage.CreateTable<AlgoEntry>();
        //await storage.CreateTable<AlgoBatch>();
    }

    private static async Task RunMacMimicWebService()
    {

        static void OnStorageSuccess(object entry, string method)
        {
        }

        static void OnStorageFailed(object entry, Exception e, string method)
        {
        }
        // mimic set env + login

        var environment = _testEnvironment;
        var userName = _testUserName;
        var accountName = _testAccountName;
        var accountType = _testAccountType;
        var password = _testPassword;
        var email = _testEmail;

        var exchange = ExchangeType.Binance;
        var symbol = "BTCUSDT";
        var quoteCode = "USDT";
        var secType = SecurityType.Fx;
        var interval = IntervalType.OneMinute;
        var fastMa = 3;
        var slowMa = 7;
        var stopLoss = 0.0002m;
        var takeProfit = 0.0005m;
        var initialFixedQuantity = 100;

        _fakeSecretFileContent = $"{new string('0', 64)}{Environment.NewLine}{new string('0', 64)}{Environment.NewLine}{email}";
        _fakeSecretFilePath = Path.Combine(Consts.DatabaseFolder, $"{Environments.ToString(environment)}_{userName}_{accountName}");

        Dependencies.Register(ExternalNames.Convert(exchange), exchange, environment);

        var securityService = Dependencies.ComponentContext.Resolve<ISecurityService>();
        var services = Dependencies.ComponentContext.Resolve<IServices>();
        var context = Dependencies.ComponentContext.Resolve<Context>();
        var core = Dependencies.ComponentContext.Resolve<Core>();
        var storage = Dependencies.ComponentContext.Resolve<IStorage>();
        storage.Success += OnStorageSuccess;
        storage.Failed += OnStorageFailed;
        var broker = ExternalNames.Convert(exchange);
        services.Admin.Initialize(environment, exchange, broker);

        var security = await securityService.GetSecurity(symbol, context.Exchange, secType);
        var result = await Login(services, userName, password, email, accountName, accountType, context.Environment, security);
        if (result != ResultCode.LoginUserAndAccountOk)
        {
            _log.Error("Login user / account failed: " + result);
            return;
        }

        AlgoEffectiveTimeRange? algoTimeRange = null;
        switch (core.Environment)
        {
            case EnvironmentType.Test:
                algoTimeRange = AlgoEffectiveTimeRange.ForBackTesting(_testStart, _testEnd);
                break;
            case EnvironmentType.Uat:
                algoTimeRange = AlgoEffectiveTimeRange.ForPaperTrading(interval);
                break;
            default:
                return;
        }

        var fiat = securityService.GetSecurity(quoteCode) ?? throw Exceptions.Impossible(quoteCode + " definition does not exist.");
        var fiatAsset = services.Portfolio.GetAssetBySecurityId(fiat.Id) ?? throw Exceptions.Impossible(quoteCode + " asset does not exist.");
        var lockedAmount = fiatAsset.Quantity - initialFixedQuantity;
        if (lockedAmount < 0) throw Exceptions.Impossible($"{quoteCode} asset quantity < {initialFixedQuantity}");

        var parameters = new AlgorithmParameters(false, interval, new List<Security> { security }, algoTimeRange);
        var algorithm = new MovingAverageCrossing(context, parameters, fastMa, slowMa, stopLoss, takeProfit);
        var screening = new SingleSecurityLogic(context, security);
        var sizing = new SimplePositionSizingLogic(PositionSizingMethod.PreserveFixed, lockedAmount: lockedAmount);
        algorithm.Screening = screening;
        algorithm.Sizing = sizing;

        _log.Info("Execute algorithm with parameters #1: " + parameters);
        _log.Info("Execute algorithm with parameters #2: " + algorithm);

        var algoBatchId = await core.Run(parameters, algorithm);

        while (true)
        {
            Thread.Sleep(5000);
        }

        await core.StopAlgorithm(algoBatchId);
    }

    private static async Task<ResultCode> Login(IServices services, string userName, string password, string email, string accountName, string accountType, EnvironmentType environment, Security security)
    {
        var result = await services.Admin.Login(userName, password, accountName, services.Context.Environment);
        if (result == ResultCode.LoginUserAndAccountOk)
        {
            return result;
        }

        switch (result)
        {
            case ResultCode.GetSecretFailed:
            case ResultCode.SecretMalformed:
                {
                    File.Delete(_fakeSecretFilePath);
                    File.WriteAllText(_fakeSecretFilePath, _fakeSecretFileContent);
                    return await Login(services, userName, password, email, accountName, accountType, environment, security);
                }
            case ResultCode.GetAccountFailed:
                {
                    _ = await CheckTestUserAndAccount(services, userName, password, email, accountName, accountType, environment);
                    return await Login(services, userName, password, email, accountName, accountType, environment, security);
                }
            default:
                return result;
        }
    }

    //private static async Task Run()
    //{
    //    Dependencies.Register(_testBroker, _testExchange, _testEnvironment);

    //    var core = Dependencies.ComponentContext.Resolve<Core>();
    //    var services = Dependencies.ComponentContext.Resolve<IServices>();

    //    var account = await CheckTestUserAndAccount(services);
    //    var security = await services.Security.GetSecurity("ETHUSDT", _testExchange, SecurityType.Fx);
    //    if (security == null)
    //    {
    //        _log.Error("Security is not found.");
    //        return;
    //    }
    //    var securityPool = new List<Security> { security };
    //    var algorithm = new MovingAverageCrossing(3, 7, 0.0005m);
    //    var screening = new SingleSecurityLogic<MacVariables>(algorithm, security);
    //    algorithm.Screening = screening;
    //    var parameters = new AlgoStartupParameters(true, _testUserName, account.Name, _testEnvironment, _testExchange, _testBroker,
    //        IntervalType.OneMinute, securityPool, AlgoEffectiveTimeRange.ForBackTesting(new DateTime(2022, 1, 1), DateTime.UtcNow));

    //    var loginResult = await services.Admin.Login(parameters.UserName, _testPassword, parameters.AccountName, parameters.Environment);
    //    if (loginResult != ResultCode.LoginUserAndAccountOk) throw new InvalidOperationException(loginResult.ToString());

    //    await core.StartAlgorithm(parameters, algorithm);
    //}

    private static async Task<Account> CheckTestUserAndAccount(IServices services, string un, string pwd, string email, string an, string at, EnvironmentType et)
    {
        var user = await services.Admin.GetUser(un, et);
        if (user == null)
        {
            var count = await services.Admin.CreateUser(un, pwd, email, et);
            user = await services.Admin.GetUser(un, et);
            if (count == 0 || user == null)
            {
                _log.Error("Failed to create test user.");
                throw new InvalidOperationException();
            }
        }
        var account = await services.Admin.GetAccount(an, et);
        if (account == null)
        {
            var count = await services.Admin.CreateAccount(new Account
            {
                Name = an,
                OwnerId = user.Id,
                Type = at,
                SubType = "",
                BrokerId = ExternalNames.GetBrokerId(_testBroker),
                CreateTime = DateTime.UtcNow,
                UpdateTime = DateTime.UtcNow,
                Environment = et,
                ExternalAccount = an,
                FeeStructure = "",
            });
            account = await services.Admin.GetAccount(an, et);
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
        dataService.NextOhlc -= OnNewOhlc;
        dataService.NextOhlc += OnNewOhlc;
        await dataService.SubscribeOhlc(security, interval);
        while (printCount < _maxPrintCount)
        {
            Thread.Sleep(100);
        }
        await dataService.UnsubscribeOhlc(security, interval);

        void OnNewOhlc(int securityId, OhlcPrice price, bool isComplete)
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

        var resultCode = await adminService.Login(_testUserName, _testPassword, _testAccountName, _testEnvironment);
        if (resultCode != ResultCode.LoginUserAndAccountOk) throw new InvalidOperationException("Login failed with code: " + resultCode);

        var security = await securityService.GetSecurity("BTCTUSD", ExchangeType.Binance, SecurityType.Fx);
        if (security == null)
            return;

        await orderService.CancelAllOpenOrders(security, OrderActionType.CleanUpLive, true);

        var order = new Order
        {
            SecurityCode = "BTCUSDT",
            SecurityId = security.Id,
            Security = security,
            Price = 10000,
            Quantity = 0.01m,
            Side = Side.Buy,
            Type = OrderType.Limit,
            TimeInForce = TimeInForceType.GoodTillCancel,
            Comment = "Demo",
        };
        await Task.Run(async () =>
        {
            await adminService.GetAccount(_testAccountName, _testEnvironment, false);
            await orderService.SendOrder(order);
        });

        while (true)
        {
            Thread.Sleep(100);
        }

        async void AfterOrderSent(Order order)
        {
            _log.Info("Order sent: " + order);
            var orders = orderService.GetOpenOrders();

            foreach (var openOrder in orders)
            {
                _log.Info("Existing Open Order: " + openOrder);
            }

            await orderService.CancelOrder(order);
        }

        static void OnNewTradesReceived(List<Trade> trades, bool isSameSecurity)
        {
            if (!isSameSecurity) throw new Exception("!");
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

        await services.Admin.Login(_testUserName, _testPassword, _testAccountName, _testEnvironment);
        var context = Dependencies.ComponentContext.Resolve<Context>();

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
                    var asset = security.EnsureCurrencyAsset();
                    var assetPosition = services.Portfolio.GetAsset(asset.Id);
                    var initQuantity = assetPosition.Quantity.ToDouble();
                    var securityPool = new List<Security> { security };

                    var algorithm = new Rumi(context, fast, slow, rumi, sl);
                    var screening = new SingleSecurityLogic(context, security);
                    algorithm.Screening = screening;

                    var engineParameters = new EngineParameters(new List<string> { "USDT" },
                                                                true, true, true, true,
                                                                new List<string> { "BTC", "USDT" });

                    var timeRange = new AlgoEffectiveTimeRange { DesignatedStart = start, DesignatedStop = end };
                    var algoParameters = new AlgorithmParameters(true, interval, securityPool, timeRange);
                    var engine = new AlgorithmEngine(context, algorithm, engineParameters);
                    await engine.Run(algoParameters);

                    var entries = engine.GetAllEntries(security.Id);
                    if (entries.IsNullOrEmpty())
                        continue;

                    if (entries.Count == 0)
                    {
                        _log.Info($"No trades at all: {security.Code} {security.Name}");
                        continue;
                    }
                    assetPosition = services.Portfolio.GetAsset(asset.Id);
                    var endQuantity = assetPosition.Quantity.ToDouble();
                    var annualizedReturn = Maths.GetAnnualizedReturn(initQuantity, endQuantity, start, end);
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
                        endQuantity,
                        Maths.GetStandardDeviation(entries.Select(e => e.Return.ToDouble()).ToList()),
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

        await services.Admin.Login(_testUserName, _testPassword, _testAccountName, _testEnvironment);
        var context = Dependencies.ComponentContext.Resolve<Context>();

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

        var columns = ColumnMappingReader.Read(typeof(AlgoEntry));

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
                    foreach (var stopLoss in stopLosses)
                    {
                        var asset = security.EnsureCurrencyAsset();
                        var assetPosition = services.Portfolio.GetAsset(asset.Id);
                        var initQuantity = assetPosition.Quantity.ToDouble();
                        var securityPool = new List<Security> { security };
                        var timeRange = new AlgoEffectiveTimeRange { DesignatedStart = start, DesignatedStop = end };
                        var algoStartParams = new AlgorithmParameters(true, interval, securityPool, timeRange);
                        var algorithm = new MovingAverageCrossing(context, algoStartParams, fast, slow, stopLoss);
                        var screening = new SingleSecurityLogic(context, security);
                        algorithm.Screening = screening;
                        var engineParameters = new EngineParameters(new List<string> { "USDT" },
                                                                    true, true, true, true,
                                                                    new List<string> { "BTC", "USDT" });
                        var engine = new AlgorithmEngine(context, algorithm, engineParameters);
                        await engine.Run(algoStartParams);

                        var entries = engine.GetAllEntries(security.Id);
                        if (entries.IsNullOrEmpty())
                            continue;

                        if (entries.Count == 0)
                        {
                            _log.Info($"No trades at all: {security.Code} {security.Name}");
                            continue;
                        }

                        assetPosition = services.Portfolio.GetAsset(asset.Id);
                        var endQuantity = assetPosition.Quantity.ToDouble();
                        var annualizedReturn = Maths.GetAnnualizedReturn(initQuantity, endQuantity, start, end);
                        if (annualizedReturn == 0)
                        {
                            _log.Info($"No trades at all: {security.Code}");
                            continue;
                        }
                        // write detail file
                        var detailFilePath = Path.Combine(folder, $"{security.Code}-{start:yyyyMMdd}-{end:yyyyMMdd}-{endQuantity}-{intervalStr}.csv");
                        Csv.Write(columns, entries, detailFilePath);

                        var tradeCount = entries.Count(e => e.LongCloseType != CloseType.None);
                        var positiveCount = entries.Where(e => e.RealizedPnl > 0).Count();
                        var summary = new List<object>
                        {
                            start,
                            end,
                            intervalStr,
                            stopLoss,
                            security.Code,
                            security.Name,
                            endQuantity,
                            Maths.GetStandardDeviation(entries.Select(e => e.Return.ToDouble()).ToList()),
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