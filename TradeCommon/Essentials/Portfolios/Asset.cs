using Common.Attributes;
using System.Diagnostics.CodeAnalysis;
using TradeCommon.Database;
using TradeCommon.Runtime;

namespace TradeCommon.Essentials.Portfolios;

[Storage("assets", DatabaseNames.ExecutionData)]
[Unique(nameof(Id))]
[Unique(nameof(SecurityId), nameof(AccountId))]
[Index(nameof(SecurityId))]
[Index(nameof(UpdateTime))]
public record Asset : SecurityRelatedEntry, IComparable<Asset>, IIdEntry
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

    /// <summary>
    /// Whether it is an empty asset without tradeable quantity.
    /// Usually it means (remaining) quantity equals to zero.
    /// If <see cref="Security.MinNotional"/> is defined (!= 0),
    /// then when quantity <= minNotional it will also be considered as closed.
    /// </summary>
    [DatabaseIgnore]
    public bool IsEmpty => (Security == null || Security.MinQuantity == 0) ? Quantity == 0 : Math.Abs(Quantity) <= Security.MinQuantity;

    /// <summary>
    /// Alias of <see cref="IsEmpty"/>
    /// </summary>
    [DatabaseIgnore]
    public bool IsClosed => IsEmpty;

    public override string ToString()
    {
        return $"[{Id}] Security[{SecurityId},{SecurityCode}], {Quantity}, {UpdateTime:yyyyMMdd-HHmmss}";
    }

    public virtual bool Equals(Asset? obj)
    {
        return obj != null && Id == obj.Id && CompareTo(obj) == 0;
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(Id,
                                AccountId,
                                CreateTime,
                                UpdateTime,
                                SecurityId,
                                Quantity,
                                LockedQuantity);
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
        return r;
    }

    public virtual bool EqualsIgnoreId(IIdEntry other)
    {
        return other is Asset asset && CompareTo(asset) == 0;
    }
}