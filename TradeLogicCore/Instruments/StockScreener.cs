using Common;
using log4net;
using TradeCommon.Constants;
using TradeCommon.Essentials;
using TradeCommon.Essentials.Instruments;
using TradeCommon.Utils.Evaluation;
using TradeDataCore;
using TradeDataCore.Instruments;
using static OfficeOpenXml.ExcelErrorValue;

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
public class StockScreener : IStockScreener
{
    private static readonly ILog _log = Common.Logger.New();

    private readonly IDataServices _dataServices;
    private readonly ISecurityService _securityService;

    public StockScreener(IDataServices dataServices, ISecurityService securityService)
    {
        _dataServices = dataServices;
        _securityService = securityService;
    }

    public async Task<SecurityScreeningResult> Filter(ExchangeType exchange, ScreeningCriteria criteria)
    {
        var securities = await _securityService.GetSecurities(exchange, SecurityType.Equity);
        if (!criteria.ExcludedCodes.IsNullOrEmpty())
        {
            securities = securities.Where(s => !criteria.ExcludedCodes.Contains(s.Code)).ToList();
        }
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
                    result.Add(new SimpleSecurity(security), value);
                }
            }
        }
        else
        {
            var toBeRanked = new List<(Security sec, double val)>();
            foreach (var security in securityCandidates)
            {
                var val = criteria.CalculateValue(_dataServices, security);
                if (double.IsNaN(val))
                    continue;
                toBeRanked.Add((security, val));
            }
            var sorted = criteria.RankingSortingType == SortingType.Ascending ?
                toBeRanked.OrderBy(t => t.val) :
                toBeRanked.OrderByDescending(t => t.val);
            if (criteria.RankingType == RankingType.TopN)
            {
                foreach (var (sec, val) in sorted.Take(criteria.RankingCount))
                {
                    result.Add(new SimpleSecurity(sec), val);
                }
            }
            else if (criteria.RankingType == RankingType.BottomN)
            {
                foreach (var (sec, val) in sorted.Skip(criteria.RankingCount - toBeRanked.Count))
                {
                    result.Add(new SimpleSecurity(sec), val);
                }
            }
        }

        _log.Info($"Screening finished. Found {criteria.RankingCount} out of {securityCandidates.Count} stocks. Criteria is {criteria}");

        return result;
    }
}

/// <summary>
/// The screening criteria class defines parameters for security screening.
/// 
/// </summary>
public abstract class ScreeningCriteria
{
    /// <summary>
    /// Checks values from a start time.
    /// </summary>
    public DateTime? StartTime { get; set; } = null;

    /// <summary>
    /// Interval type which is the length of OHLC price.
    /// </summary>
    public IntervalType IntervalType { get; set; } = IntervalType.Unknown;

    /// <summary>
    /// Security type for screening.
    /// </summary>
    public SecurityType SecurityType { get; set; } = SecurityType.Unknown;

    /// <summary>
    /// Security codes to be excluded from screening results.
    /// </summary>
    public List<string> ExcludedCodes { get; } = new();

    /// <summary>
    /// End time for the indicator calculation.
    /// It should be the <see cref="StartTime"/> of the last OHLC price.
    /// </summary>
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
    /// Gets / sets whether the criteria is related to security return.
    /// If yes, it needs one more data point (vs <see cref="LookBackPeriod"/>) to calculate the return.
    /// </summary>
    public bool IsRelatedToReturn { get; set; } = true;

    /// <summary>
    /// When checking values for a past period of time, values are aggregated into a single value for checking.
    /// </summary>
    public Func<IList<double>, double>? Calculator { get; set; }

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

    public override string ToString()
    {
        return base.ToString();
    }

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

public class SecurityScreeningResult : List<SecurityScreeningResult.Pair>
{
    public record Pair(SimpleSecurity Security, double Value);

    public void Add(SimpleSecurity simpleSecurity, double value)
    {
        Add(new Pair(simpleSecurity, value));
    }
}