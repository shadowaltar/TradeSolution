using Common.Attributes;
using System.Diagnostics.CodeAnalysis;
using TradeCommon.Database;
using TradeCommon.Essentials.Instruments;
using TradeCommon.Essentials.Trading;
using TradeCommon.Runtime;

namespace TradeCommon.Essentials.Portfolios;

/// <summary>
/// An asset entry in an account (one account may hold multiple asset entries).
/// </summary>

[Storage("assets", DatabaseNames.ExecutionData)]
[Unique(nameof(Id))]
[Unique(nameof(SecurityId), nameof(AccountId))]
[Index(nameof(SecurityId))]
[Index(nameof(CreateTime))]
public record Asset : SecurityRelatedEntry, IComparable<Asset>, ITimeBasedUniqueIdEntry
{
    /// <summary>
    /// Unique id of this asset asset.
    /// </summary>
    [NotNull, Positive]
    public long Id { get; set; } = 0;

    /// <summary>
    /// The associated account's Id.
    /// </summary>
    public int AccountId { get; set; } = 0;

    public DateTime CreateTime { get; set; }
    public DateTime UpdateTime { get; set; }

    /// <summary>
    /// Amount of asset being held which is free to use.
    /// </summary>
    public decimal Quantity { get; set; }

    /// <summary>
    /// Amount of asset being held which is locked by broker.
    /// </summary>
    public decimal LockedQuantity { get; set; }

    public decimal StrategyLockedQuantity { get; set; }

    public override string ToString()
    {
        return $"[{Id}] Security[{SecurityId},{SecurityCode}], {Quantity}, {UpdateTime:yyyyMMdd-HHmmss}";
    }

    public virtual bool Equals(Asset? obj)
    {
        if (obj == null)
            return false;

        if (Id == obj.Id
            && AccountId == obj.AccountId
            && CreateTime == obj.CreateTime
            && UpdateTime == obj.UpdateTime
            && SecurityId == obj.SecurityId
            && Quantity == obj.Quantity
            && LockedQuantity == obj.LockedQuantity
            && StrategyLockedQuantity == obj.StrategyLockedQuantity)
            return true;
        return false;
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(Id,
                                AccountId,
                                CreateTime,
                                UpdateTime,
                                SecurityId,
                                Quantity,
                                LockedQuantity,
                                StrategyLockedQuantity);
    }

    public virtual int CompareTo(Asset? other)
    {
        if (other == null) return 1;
        var r = AccountId.CompareTo(other.AccountId);
        if (r == 0) r = CreateTime.CompareTo(other.CreateTime);
        if (r == 0) r = UpdateTime.CompareTo(other.UpdateTime);
        if (r == 0) r = SecurityId.CompareTo(other.SecurityId);
        if (r == 0) r = SecurityCode.CompareTo(other.SecurityCode);
        if (r == 0) r = Quantity.CompareTo(other.Quantity);
        if (r == 0) r = LockedQuantity.CompareTo(other.LockedQuantity);
        if (r == 0) r = StrategyLockedQuantity.CompareTo(other.StrategyLockedQuantity);
        return r;
    }

    public virtual bool EqualsIgnoreId(ITimeBasedUniqueIdEntry other)
    {
        if (other is not Asset asset) return false;
        return CompareTo(asset) == 0;
    }
}
