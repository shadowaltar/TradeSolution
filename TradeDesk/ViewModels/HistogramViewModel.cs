using Common;
using System.Collections.Generic;

namespace TradeDesk.ViewModels;
public class HistogramViewModel : AbstractViewModel
{
    public void Show(List<decimal> data, int bucketCount)
    {
        foreach (var bucket in data.Split(bucketCount))
        {
            var count = bucket.Count;
        }
    }
}