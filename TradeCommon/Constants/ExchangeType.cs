using System.ComponentModel;

namespace TradeCommon.Constants;
public enum ExchangeType
{
    [Description(ExternalNames.Unknown)]
    Unknown = 0,

    [Description(ExternalNames.Hkex)]
    Hkex,

    [Description(ExternalNames.Binance)]
    Binance,

    [Description(ExternalNames.Okex)]
    Okex,

    [Description(ExternalNames.Simulator)]
    Simulator,
}
