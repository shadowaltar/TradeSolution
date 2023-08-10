using Autofac;
using Common;
using log4net.Config;
using OfficeOpenXml;
using System.Text;
using TradeCommon.Constants;
using TradeCommon.Database;
using TradeCommon.Essentials;
using TradeCommon.Essentials.Instruments;
using TradeDataCore;
using TradeDataCore.Importing;
using TradeDataCore.Instruments;

internal class Program
{
    /// <summary>
    /// Run the data program.
    /// </summary>
    /// <param name="mode">Only one mode is supported: periodic-price-saver</param>
    /// <param name="external">External system name. Supported values: Binance, Yahoo.</param>
    /// <param name="interval">Price interval. Supported values: OneMinute, OneHour, OneDay. Or leave this optional arg in order to run all three intervals.</param>
    /// <returns></returns>
    public static async Task Main(string[] args)
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        XmlConfigurator.Configure();
        ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
        Dependencies.Register();

        //await PeriodicDataImporting(args);
        var ss = Dependencies.Container.Resolve<ISecurityService>();
        var reader = new JsonPriceReader(ss);
        var results = await reader.Import(@"C:\Temp\AllPrices_1h_20210808_binance\AllPrices_1h_20210808_binance.json");
    }

    private static async Task PeriodicDataImporting(string[] args)
    {
        if (args.Length != 3)
        {
            await Console.Out.WriteLineAsync("Requires Mode, External and Interval arguments.");
            return;
        }
        string mode = args[0];
        string external = args[1];
        string interval = args[2];

        var intervals = new List<IntervalType> { IntervalType.OneMinute, IntervalType.OneHour, IntervalType.OneDay };
        var intervalType = IntervalTypeConverter.Parse(interval);
        if (intervalType is IntervalType.OneMinute or IntervalType.OneHour or IntervalType.OneDay
            or IntervalType.Unknown)
        {
            if (intervalType == IntervalType.Unknown)
            {
                await Console.Out.WriteLineAsync("Interval type is Unknown so it will be defaulted to " + string.Join(", ", intervals));
            }
            else
            {
                intervals.Clear();
                intervals.Add(intervalType);
                await Console.Out.WriteLineAsync("Interval type is " + intervalType);
            }
        }
        else
        {
            await Console.Out.WriteLineAsync("Interval type is " + intervalType + " which is not supported.");
            return;
        }

        IHistoricalPriceReader priceReader;
        if (external.EqualsIgnoreCase(ExternalNames.Binance))
        {
            Dependencies.Register(ExternalNames.Binance);
            priceReader = new TradeDataCore.Importing.Binance.HistoricalPriceReader();
        }
        else if (external.EqualsIgnoreCase(ExternalNames.Yahoo))
        {
            priceReader = new TradeDataCore.Importing.Yahoo.HistoricalPriceReader();
        }
        else
        {
            await Console.Out.WriteLineAsync("Invalid external name. Must be either Binance or Yahoo.");
            return;
        }

        if (mode != "periodic-price-saver")
        {
            await Console.Out.WriteLineAsync("Mode must be periodic-price-saver");
            return;
        }

        var securityService = Dependencies.Container.Resolve<ISecurityService>();
        var results = new List<(IntervalType i, int securityId, int count)>();
        var start = DateTime.UtcNow.Date.AddDays(-2);
        var end = DateTime.UtcNow;
        var securities = await securityService.GetSecurities(ExchangeType.Binance, SecurityType.Fx);

        foreach (var i in intervals)
        {
            var allPrices = await priceReader.ReadPrices(securities, start, end, i);
            foreach (var security in securities)
            {
                if (allPrices?.TryGetValue(security.Id, out var list) ?? false)
                {
                    var (securityId, count) = await Storage.UpsertPrices(security.Id, i, SecurityType.Fx, list);
                    results.Add((i, securityId, count));
                }
            }
        }
        await Console.Out.WriteLineAsync(Json.ToJson(results));


    }
}