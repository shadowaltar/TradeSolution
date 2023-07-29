﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TradeCommon.Essentials.Instruments;

namespace TradeCommon.Constants;

public enum DataType
{
    Unknown,
    OrderBook,
    Tick,
    OhlcPrice,
    FinancialStat,

    Order,
    Trade,
    Position,
}

public class DataTypeConverter
{
    public static DataType Parse(string? str)
    {
        if (str == null)
            return DataType.Unknown;

        str = str.Trim().ToUpperInvariant();

        return str switch
        {
            "ORDERBOOK" or "DEPTHBOOK" => DataType.OrderBook,
            "ORDER" => DataType.Order,
            "TRADE" => DataType.Trade,
            "POSITION" => DataType.Position,
            _ => DataType.Unknown,
        };
    }

    public static bool Matches(string typeStr, DataType type) => Parse(typeStr) == type;
}