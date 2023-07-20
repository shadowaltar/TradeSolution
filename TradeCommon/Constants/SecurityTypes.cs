using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TradeCommon.Essentials.Instruments;

namespace TradeCommon.Constants;
public static class SecurityTypes
{
    public static readonly IList<string> StockTypes = new string[] {
        "EQUITY", "STOCK", "ADR",
        "REAL ESTATE INVESTMENT TRUSTS", "REITS", "REIT",
        "EXCHANGE TRADED PRODUCTS", "ETP"};

    public const string Fx = "FX";
    public const string Crypto = "CRYPTO";
    public const string Future = "FUTURE";
    public const string Futures = "FUTURES";
    public const string Forward = "FORWARD";
    public const string Option = "OPTION";

}
