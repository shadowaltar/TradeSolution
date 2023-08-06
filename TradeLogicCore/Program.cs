using Autofac;
using Common;
using log4net;
using log4net.Config;
using OfficeOpenXml;
using System.Text;
using TradeCommon.Constants;
using TradeCommon.Essentials;
using TradeCommon.Essentials.Instruments;
using TradeCommon.Essentials.Quotes;
using TradeCommon.Essentials.Trading;
using TradeCommon.Reporting;
using TradeDataCore.Instruments;
using TradeDataCore.MarketData;
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

        await RunRumiBackTestDemo();

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
        var portfolioService = Dependencies.ComponentContext.Resolve<IPortfolioService>();
        var orderService = Dependencies.ComponentContext.Resolve<IOrderService>();
        var tradeService = Dependencies.ComponentContext.Resolve<ITradeService>();
        var securityService = Dependencies.ComponentContext.Resolve<ISecurityService>();
        orderService.OrderAcknowledged += OnOrderAck;
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

        void OnOrderAck(Order order)
        {
            Console.WriteLine(order);

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

        var securities = await securityService.GetSecurities(ExchangeType.Binance, SecurityType.Fx);
        securities = securities.Where(s => s.Code is "BTCUSDT" or "BTCTUSD").ToList();

        var stopLosses = new List<decimal> { 0.02m };
        var intervalTypes = new List<IntervalType> { IntervalType.OneDay, IntervalType.OneHour };

        var resultMatrix = new List<List<object>>();

        // TODO
        var x = ColumnMappingReader.Read(typeof(RuntimePosition<RumiVariables>));

        var start = new DateTime(2022, 1, 1);
        var end = new DateTime(2023, 6, 30);
        foreach (var security in securities)
        {
            foreach (var interval in intervalTypes)
            {
                foreach (var sl in stopLosses)
                {
                    security.PriceDecimalPoints = 6;
                    var algo = new Rumi(mds) { StopLossRatio = sl, FastParam = 2, SlowParam = 5, RumiParam = 1 };
                    var initCash = 1000;
                    var entries = await algo.BackTest(security, interval, start, end, initCash);
                    if (entries.IsNullOrEmpty())
                        continue;

                    var intervalStr = IntervalTypeConverter.ToIntervalString(interval);
                    var filePath = Path.Combine(@"C:\Temp", $"Rumi_{security.Code}_{intervalStr}_{start:yyyyMMdd}_{end:yyyyMMdd}_{DateTime.UtcNow:MMddHHmmss}.xlsx");
                    new ExcelWriter()
                        .WriteSheet<RuntimePosition<RumiVariables>>("BackTest", entries)
                        .Save(filePath);

                    var result = new List<object>
                    {
                        security.Code,
                        intervalStr,
                        algo.FreeCash,
                        filePath,
                    };

                    _log.Info($"Result: {string.Join('|', result)}");
                    resultMatrix.Add(result);
                }
            }
        }

        _log.Info("RESULTS ----------");
        _log.Info(Printer.Print(resultMatrix));
        
    }
}