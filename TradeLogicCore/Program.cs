using Autofac;
using Common;
using log4net;
using log4net.Config;
using System.Text;
using TradeCommon.Constants;
using TradeCommon.Essentials;
using TradeCommon.Essentials.Instruments;
using TradeCommon.Essentials.Quotes;
using TradeDataCore.Instruments;
using TradeDataCore.MarketData;
using TradeLogicCore;

internal class Program
{
    private static int _printCount = 0;
    private static int _maxPrintCount = 10;
    private static Security _security;

    private static async Task Main(string[] args)
    {
        ILog log = Logger.New();

        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        XmlConfigurator.Configure();

        Dependencies.Register(ExternalNames.Binance);

        var securityService = Dependencies.ComponentContext.Resolve<ISecurityService>();
        _security = await securityService.GetSecurity("BTCUSDT", ExchangeType.Binance, SecurityType.Fx);

        var dataService = Dependencies.ComponentContext.Resolve<IRealTimeMarketDataService>();
        await dataService.Initialize();
        dataService.NewOhlc += OnNewOhlc;
        dataService.SubscribeOhlc(_security);

        while (_printCount < _maxPrintCount)
        {
            Thread.Sleep(100);
        }

        await dataService.UnsubscribeOhlc(_security);

        Console.WriteLine("Finished.");
    }

    private static void OnNewOhlc(int securityId, OhlcPrice price)
    {
        _printCount++;
        if (_security.Id != securityId)
        {
            Console.WriteLine("Impossible!");
            return;
        }
        Console.WriteLine(price);
    }

}