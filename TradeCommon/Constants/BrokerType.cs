using System.ComponentModel;

namespace TradeCommon.Constants;

public enum BrokerType
{
    [Description(ExternalNames.Simulator)]
    Simulator = -1,

    [Description(ExternalNames.Unknown)]
    Unknown = 0,

    [Description(ExternalNames.Unknown)]
    Any = 0,

    [Description(ExternalNames.Binance)]
    Binance,

    [Description(ExternalNames.Okex)]
    Okex,

    [Description(ExternalNames.Futu)]
    Futu,
}
