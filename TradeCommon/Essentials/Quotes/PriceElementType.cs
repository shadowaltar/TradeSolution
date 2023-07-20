using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TradeCommon.Essentials.Quotes;
public enum PriceElementType
{
    Default,
    Open,
    High,
    Low,
    Close,
    Volume,
    Typical4, // (O+H+L+C)/3
    Typical3, // (H+L+C)/3
}
