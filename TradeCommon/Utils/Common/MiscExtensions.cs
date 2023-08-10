using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TradeCommon.Utils.Common;
public static class MiscExtensions
{
    public static T Cast<T>(this object obj)
    {
        return (T)(object)obj;
    }
}
