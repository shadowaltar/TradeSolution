using Common;
using TradeCommon.Essentials.Instruments;
using TradeCommon.Essentials.Quotes;
using TradeDataCore;
using TradeDataCore.Instruments;

namespace TradeLogicCore.Instruments;

public class OhlcPriceScreeningCriteria : ScreeningCriteria
{
    public PriceElementType ElementType { get; set; } = PriceElementType.Close;

    public override double CalculateValue(ISecurityService securityService, Security security)
    {
        double Selector(OhlcPrice d) => OhlcPrice.PriceElementSelectors[ElementType](d).ToDouble();

        IList<double>? values = null;
        if (StartTime != null)
        {
            var actualStart = IsRelatedToReturn ? StartTime.Value.AddBusinessDays(-1) : StartTime.Value;
            var prices = AsyncHelper.RunSync(() => securityService.GetOhlcPrices(security, IntervalType, actualStart, EndTime));
            values = prices.Select(Selector).ToList();
        }
        else if (StartTime == null && LookBackPeriod != null)
        {
            var actualLookBack = LookBackPeriod.Value + (IsRelatedToReturn ? 1 : 0);
            var prices = AsyncHelper.RunSync(() => securityService.GetOhlcPrices(security, IntervalType, EndTime, actualLookBack));
            values = prices.Select(Selector).ToList();
        }
        if (values != null)
        {
            return Calculator?.Invoke(values) ?? double.NaN;
        }
        return double.NaN;
    }
}
