using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TradeDataCore.Importing.Yahoo
{
    public class ListedOptionReader
    {
        public static async Task ReadUnderlyingSummary()
        {
            const string url = @"https://query1.finance.yahoo.com/v7/finance/options/{0}";
        }
    }
}
