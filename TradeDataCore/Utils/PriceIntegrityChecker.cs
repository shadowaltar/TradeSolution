using Common;
using TradeCommon.Essentials;
using TradeCommon.Essentials.Instruments;
using TradeDataCore.Instruments;

namespace TradeDataCore.Utils;

public class PriceIntegrityChecker
{
    private readonly ISecurityService _securityService;

    public PriceIntegrityChecker(ISecurityService securityService)
    {
        _securityService = securityService;
    }

    public async Task<List<DateTime>> CheckMissingPrices(Security security, DateTime start, DateTime end, IntervalType intervalType)
    {
        var missingOnes = new List<DateTime>();
        var timeSpan = IntervalTypeConverter.ToTimeSpan(intervalType);
        var timeGroups = DateUtils.CreateEqualLengthTimeIntervals(start, end, timeSpan * 100);
        foreach (var (s, e) in timeGroups)
        {
            var expectedCount = (e.AddMilliseconds(1) - s) / timeSpan;
            var prices = await _securityService.ReadPrices(security.Id, intervalType, SecurityTypeConverter.Parse(security.Type), s, e);
            if (prices.Count == 0 && expectedCount != 0)
            {
                missingOnes.AddRange(DateUtils.CreateEqualGapTimePoints(s, e, timeSpan, false));
                continue;
            }
            if (prices.Count == 1 && expectedCount == 1)
                continue;
            var lastPrice = prices[0];
            for (int i = 1; i < prices.Count; i++)
            {
                if (prices[i].T - lastPrice.T != timeSpan)
                {
                    missingOnes.AddRange(DateUtils.CreateEqualGapTimePoints(lastPrice.T, prices[i].T, timeSpan, false));
                }
                lastPrice = prices[i];
            }
        }
        return missingOnes;
    }
}
