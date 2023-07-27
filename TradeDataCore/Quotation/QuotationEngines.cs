using Common;
using log4net;
using TradeCommon.Externals;
using TradeCommon.Runtime;

namespace TradeDataCore.Quotation;
public class QuotationEngines
{
    private static readonly ILog _log = Logger.New();

    public IExternalQuotationManagement? QuotationEngine { get; private set; }

    public QuotationEngines(IExternalQuotationManagement quotationEngine)
    {
        QuotationEngine = quotationEngine;
    }

    public async Task Initialize()
    {
        QuotationEngine = QuotationEngine ?? throw new InvalidOperationException();

        await QuotationEngine.InitializeAsync();
        _log.Info($"Initialized {QuotationEngine.Name} quotation engine.");
    }
}
