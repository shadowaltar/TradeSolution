using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TradeCommon.Calculations;
public class ValueCrossing
{
    public static bool TryCheck(decimal last1, decimal current1, decimal last2, decimal current2, out int crossing)
    {
        if (last1 < last2 && current1 > current2)
        {
            // 1 cross above 2
            crossing = 1;
            return true;
        }
        else if (last1 > last2 && current1 < current2)
        {
            // 1 cross below 2
            crossing = -1;
            return true;
        }
        crossing = 0;
        return false;
    }
}
