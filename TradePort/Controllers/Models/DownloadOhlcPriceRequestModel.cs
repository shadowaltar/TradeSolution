using System.ComponentModel;
using TradeCommon.Constants;
using TradeCommon.Essentials;
using TradeCommon.Runtime;

namespace TradePort.Controllers.Models;

public record DownloadOhlcPriceRequestModel
{
    [DefaultValue(ExchangeType.Binance)]
    public ExchangeType Exchange { get; set; }

    [DefaultValue(EnvironmentType.Prod)]
    public EnvironmentType Environment { get; set; }

    [DefaultValue("BTCFDUSD")]
    public string SecurityCode { get; set; } = "BTCFDUSD";

    [DefaultValue(IntervalType.OneMinute)]
    public IntervalType Interval { get; set; } = IntervalType.OneMinute;

    [DefaultValue("20230101")]
    public string StartDateTime { get; set; } = "20230801";

    [DefaultValue("20231231")]
    public string EndDateTime { get; set; } = "20231120";
}