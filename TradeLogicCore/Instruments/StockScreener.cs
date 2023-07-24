using log4net;
using TradeCommon.Constants;
using TradeCommon.Essentials;
using TradeCommon.Essentials.Instruments;
using TradeCommon.Essentials.Quotes;
using TradeCommon.Utils.Evaluation;
using TradeDataCore;
using TradeDataCore.Instruments;

namespace TradeLogicCore.Instruments;

/// <summary>
/// A stock screener worker class.
/// Simple screening filters:
/// * on average: stdev of return > x over past n days
/// * lastest data point only: p/e or d/e ratio within a certain range from x to y
/// * always: market-cap > x
/// * always: within a particular industry
/// 
/// Universal screening filters:
/// * on average for last n days: best or worst performer
/// </summary>
public class StockScreener : IStockScreener, ISecurityScreener
{
    private static readonly ILog _log = Common.Logger.New();

    private readonly IDataServices _dataServices;
    private readonly ISecurityService _securityService;

    public Timer PeriodicTimer { get; private set; }

    public StockScreener(IDataServices dataServices, ISecurityService securityService)
    {
        _dataServices = dataServices;
        _securityService = securityService;
    }

    public void StartFilterPeriodically(IntervalType type, int multiplier = 1)
    {
        StopFilterPeriodically();

        var interval = IntervalTypeConverter.ToTimeSpan(type).Multiply(multiplier);
        PeriodicTimer ??= new Timer(OnFilterTriggered, null, TimeSpan.Zero, interval);
        _log.Info("Started stock screener periodically: every " + interval);
    }

    public void StopFilterPeriodically()
    {
        PeriodicTimer?.Dispose();
        _log.Info("Stopped stock screener.");
    }

    private void OnFilterTriggered(object? state)
    {
        throw new NotImplementedException();
    }


    public async Task<SecurityScreeningResult> Filter(ExchangeType exchange, ScreeningCriteria criteria)
    {
        var securities = await _securityService.GetSecurities(exchange, SecurityType.Equity);
        return Filter(securities, criteria);
    }

    public SecurityScreeningResult Filter(List<Security> securityCandidates, ScreeningCriteria criteria)
    {
        var result = new SecurityScreeningResult();
        var goodOnes = new List<Security>();
        var goodOneValues = new List<double>();

        if (criteria.RankingType == RankingType.None)
        {
            foreach (var security in securityCandidates)
            {
                var value = criteria.CalculateValue(_dataServices, security);

                if (criteria.Compare(value))
                {
                    result.Securities.Add(security);
                    result.AssociatedValues.Add(value);
                }
            }
        }
        else
        {
            var toBeRanked = new List<(Security sec, double val)>();
            foreach (var security in securityCandidates)
            {
                var val = criteria.CalculateValue(_dataServices, security);

                toBeRanked.Add((security, val));
            }
            var sorted = criteria.RankingSortingType == SortingType.Ascending ?
                toBeRanked.OrderBy(t => t.val) :
                toBeRanked.OrderByDescending(t => t.val);
            if (criteria.RankingType == RankingType.TopN)
            {
                foreach (var (sec, val) in sorted.Take(criteria.RankingCount))
                {
                    result.Securities.Add(sec);
                    result.AssociatedValues.Add(val);
                }
            }
        }
        return result;
    }
}

public abstract class ScreeningCriteria
{
    /// <summary>
    /// Checks values from a start time.
    /// </summary>
    public DateTime? StartTime { get; set; } = null;

    public DateTime EndTime { get; set; }

    /// <summary>
    /// Checks values for the past period of time.
    /// <list type="table">
    /// 
    /// <listheader>
    ///     <term>case</term>
    ///     <description>description</description>
    /// </listheader>
    /// <item>
    ///     <term><see cref="LookBackPeriod"/> == null and <see cref="StartTime"/> != null</term>
    ///     <description>values to be used are from <see cref="StartTime"/> to <see cref="EndTime"/>.</description>
    /// </item>
    /// <item>
    ///     <term><see cref="LookBackPeriod"/> == null and <see cref="StartTime"/> == null</term>
    ///     <description>only care about latest entry.</description>
    /// </item>
    /// </list>
    /// </summary>
    public int? LookBackPeriod { get; set; } = null;

    /// <summary>
    /// When checking values for a past period of time, values are aggregated into a single value for checking.
    /// </summary>
    public Func<IList<double>, double>? Aggregator { get; set; }

    /// <summary>
    /// When checking values for a past period of time, <see cref="OhlcPrice"/>s are aggregated into a single value for checking.
    /// Only one of <see cref="Aggregator"/> and this will be effective.
    /// </summary>
    public Func<IList<OhlcPrice>, double>? OhlcAggregator { get; set; }

    /// <summary>
    /// The operator to compare the value for checking vs the benchmark value.
    /// </summary>
    public ComparisonOp ComparisonOp { get; set; }

    /// <summary>
    /// The benchmark value to be checked against the value for checking. 
    /// </summary>
    public double BenchmarkValue { get; set; } = double.NaN;

    /// <summary>
    /// Used in ranking mode only.
    /// </summary>
    public RankingType RankingType { get; set; } = RankingType.None;

    /// <summary>
    /// Used in ranking mode only.
    /// </summary>
    public int RankingCount { get; set; } = 0;

    /// <summary>
    /// Used in ranking mode only.
    /// </summary>
    public SortingType RankingSortingType { get; set; } = SortingType.Ascending;

    public abstract double CalculateValue(IDataServices dataServices, Security security);

    public bool Compare(double val)
    {
        switch (ComparisonOp)
        {
            case ComparisonOp.Equals:
                if (val == BenchmarkValue)
                {
                    return true;
                }
                break;
            case ComparisonOp.GreaterThan:
                if (val > BenchmarkValue)
                {
                    return true;
                }
                break;
            case ComparisonOp.GreaterThanOrEquals:
                if (val >= BenchmarkValue)
                {
                    return true;
                }
                break;
            case ComparisonOp.LessThan:
                if (val < BenchmarkValue)
                {
                    return true;
                }
                break;
            case ComparisonOp.LessThanOrEquals:
                if (val <= BenchmarkValue)
                {
                    return true;
                }
                break;
        }
        return false;
    }
}

public class OhlcPriceScreeningCriteria : ScreeningCriteria
{
    public PriceElementType ElementType { get; }
    public IntervalType IntervalType { get; }

    public override double CalculateValue(IDataServices dataServices, Security security)
    {
        double Selector(OhlcPrice d) => decimal.ToDouble(OhlcPrice.PriceElementSelectors[ElementType](d));

        IList<double>? values = null;
        if (StartTime != null)
        {
            values = dataServices.GetOhlcPrices(security, IntervalType, StartTime.Value, EndTime).Select(Selector).ToList();
        }
        else if (StartTime == null && LookBackPeriod != null)
        {
            values = dataServices.GetOhlcPrices(security, IntervalType, EndTime, LookBackPeriod.Value).Select(Selector).ToList();
        }
        if (values != null)
        {
            return Aggregator?.Invoke(values) ?? double.NaN;
        }
        return double.NaN;
    }
}

public class SecurityScreeningResult
{
    public List<Security> Securities { get; } = new();
    public List<double> AssociatedValues { get; } = new();
}