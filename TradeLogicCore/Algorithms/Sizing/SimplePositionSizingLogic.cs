using Common;
using log4net;
using TradeCommon.Essentials.Algorithms;
using TradeCommon.Runtime;
using TradeLogicCore.Algorithms.Abnormality;
using TradeLogicCore.Algorithms.Sizing;

namespace TradeLogicCore.Algorithms;

public class SimplePositionSizingLogic : IPositionSizingAlgoLogic
{
    private static readonly ILog _log = Logger.New();

    public AverageAbnormalityDetector SizeAbnormalityDetector { get; } = new(0.2M, 5);

    public PositionSizingMethod SizingMethod { get; }
    public decimal RelativeMin { get; }
    public decimal AbsoluteMin { get; }
    public decimal RelativeMax { get; }
    public decimal AbsoluteMax { get; }
    public decimal FixedAmount { get; }
    public decimal LockedAmount { get; }

    public SimplePositionSizingLogic(PositionSizingMethod sizingMethod = PositionSizingMethod.All,
                                     decimal? relativeMin = null,
                                     decimal? absoluteMin = null,
                                     decimal? relativeMax = null,
                                     decimal? absoluteMax = null,
                                     decimal? fixedAmount = null,
                                     decimal? lockedAmount = null)
    {
        if (sizingMethod == PositionSizingMethod.Fixed && !fixedAmount.IsValid())
        {
            throw new InvalidOperationException("Must specify a fixed amount for Constant method.");
        }
        else
        {
            if (relativeMax.IsValid() && relativeMin.IsValid())
            {
                if (relativeMax < relativeMin)
                {
                    throw new InvalidOperationException("Relative min size must be smaller than relative max size.");
                }
            }
            if (relativeMax.IsValid() && (relativeMax < 0 || relativeMax > 1))
            {
                throw new InvalidOperationException("Relative max size must be between 0 and 1.");
            }
            if (relativeMin.IsValid() && (relativeMin < 0 || relativeMin > 1))
            {
                throw new InvalidOperationException("Relative max size must be between 0 and 1.");
            }

            if (absoluteMax.IsValid() && absoluteMin.IsValid())
            {
                if (absoluteMax < absoluteMin)
                {
                    throw new InvalidOperationException("Absolute min size must be smaller than absolute max size.");
                }
            }
        }
        SizingMethod = sizingMethod;
        RelativeMin = relativeMin ?? decimal.MinValue;
        AbsoluteMin = absoluteMin ?? decimal.MinValue;
        RelativeMax = relativeMax ?? decimal.MinValue;
        AbsoluteMax = absoluteMax ?? decimal.MinValue;
        FixedAmount = fixedAmount ?? decimal.MinValue;
        LockedAmount = lockedAmount ?? decimal.MinValue;
    }

    public decimal GetSize(decimal freeAmount, AlgoEntry current, AlgoEntry? last, decimal price, DateTime time)
    {
        if (current.Security == null) throw Exceptions.MissingSecurity();
        if (price == 0) return 0;

        var size = GetTradingAmount(freeAmount) / price;
        var actualSize = current.Security.RoundLotSize(size);
        if (!SizeAbnormalityDetector.IsNormal(actualSize))
        {
            _log.Error($"Erroneous trading size: {actualSize}; previous {SizeAbnormalityDetector.Count} items' average is {SizeAbnormalityDetector.LastAverage}");
            return 0;
        }
        return actualSize;
    }

    private decimal GetTradingAmount(decimal freeAmount)
    {
        if (AbsoluteMin.IsValid() && freeAmount < AbsoluteMin)
        {
            _log.Warn($"Free amount {freeAmount} is smaller than required abs min amount {AbsoluteMin}. Returns 0.");
            return 0;
        }

        switch (SizingMethod)
        {
            case PositionSizingMethod.All:
                var max = decimal.MinValue;
                var max1 = decimal.MinValue;
                var max2 = decimal.MinValue;
                if (RelativeMax.IsValid())
                    max1 = freeAmount * RelativeMax;
                if (AbsoluteMax.IsValid())
                    max2 = AbsoluteMax;
                if (max1.IsValid() && max2.IsValid())
                    max = Math.Max(max1, max2);
                else if (max1.IsValid())
                    max = max1;
                else if (max2.IsValid())
                    max = max2;
                if (max.IsValid())
                    return Math.Max(freeAmount, max);
                return freeAmount;
            case PositionSizingMethod.AsSmallAsPossible:
                var min = decimal.MinValue;
                var min1 = decimal.MinValue;
                var min2 = decimal.MinValue;
                if (RelativeMin.IsValid())
                    min1 = freeAmount * RelativeMin;
                if (AbsoluteMin.IsValid())
                    min2 = AbsoluteMin;
                if (min1.IsValid() && min2.IsValid())
                    min = Math.Min(min1, min2);
                else if (min1.IsValid())
                    min = min1;
                else if (min2.IsValid())
                    min = min2;
                if (min.IsValid())
                    return Math.Min(freeAmount, min);
                return 0;
            case PositionSizingMethod.Fixed:
                if (FixedAmount.IsValid())
                    return Math.Min(FixedAmount, freeAmount);
                throw new InvalidOperationException("Must specify a fixed amount for Fixed sizing method.");
            case PositionSizingMethod.PreserveFixed:
                if (LockedAmount.IsValid())
                {
                    var free = freeAmount - LockedAmount;
                    if (free <= 0)
                    {
                        _log.Warn($"Expect to lock away {LockedAmount} but the free amount is just {freeAmount}. Returns zero.");
                        return 0;
                    }
                    return free;
                }
                throw new InvalidOperationException("Must specify a locked amount for PreseveFixed sizing method.");
            case PositionSizingMethod.Zero:
                return 0;
            default:
                throw new InvalidOperationException("Invalid position sizing method.");
        }
    }
}

public enum PositionSizingMethod
{
    Unknown,
    Zero,
    Fixed,
    All,
    PreserveFixed,// always preserve fixed amount
    AsSmallAsPossible,
}