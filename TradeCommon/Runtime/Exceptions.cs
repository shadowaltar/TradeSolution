using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TradeCommon.Runtime;

public static class Exceptions
{
    public static InvalidOperationException ThrowMissingQuoteAsset(string securityCode)
    {
        return new InvalidOperationException($"The security {securityCode} must have an associated currency/quote asset. Check your security definition.");
    }
}
