using Common.Attributes;
using System.Diagnostics.CodeAnalysis;
using TradeCommon.Database;
using TradeCommon.Essentials.Instruments;
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
public record Asset : ITimeBasedUniqueIdEntry, ISecurityRelatedEntry
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
    /// The asset security id.
    /// </summary>
    public int SecurityId { get; set; } = 0;

    /// <summary>
    /// Asset's asset security code, like USD, EUR, BTC etc. 
    /// </summary>
    public string? SecurityCode { get; set; } = null;

    [DatabaseIgnore]
    public Security Security { get; set; }

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
}
