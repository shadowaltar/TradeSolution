using Common;
using Common.Attributes;
using System.Diagnostics.CodeAnalysis;
using TradeCommon.Database;
using TradeCommon.Runtime;

namespace TradeCommon.Essentials.Portfolios;

/// <summary>
/// A state snapshot of asset entry in an account (one account may hold multiple asset entries).
/// </summary>

[Storage("asset_states", DatabaseNames.ExecutionData)]
[Unique(nameof(Id))]
[Unique(nameof(SecurityId), nameof(Time), nameof(AccountId))]
[Index(nameof(SecurityId))]
[Index(nameof(Time))]
public record AssetState : SecurityRelatedEntry, IComparable<AssetState>, IIdEntry
{
    [DatabaseIgnore]
    private static readonly IdGenerator _assetStateIdGen = IdGenerators.Get<AssetState>();

    /// <summary>
    /// Unique id of this asset asset.
    /// </summary>
    [NotNull, Positive]
    public long Id { get; set; } = 0;

    /// <summary>
    /// The associated account's Id.
    /// </summary>
    public int AccountId { get; set; } = 0;

    /// <summary>
    /// The asset state's time.
    /// </summary>
    public DateTime Time { get; set; }

    /// <summary>
    /// Amount of asset.
    /// </summary>
    public decimal Quantity { get; set; }

    public static AssetState From(Asset asset)
    {
        return new AssetState
        {
            Id = _assetStateIdGen.NewTimeBasedId,
            SecurityId = asset.SecurityId,
            Security = asset.Security,
            SecurityCode = asset.SecurityCode,
            AccountId = asset.AccountId,
            Quantity = asset.Quantity,
            Time = asset.UpdateTime,
        };
    }

    public override string ToString()
    {
        return $"AssetState: {Id}, SecurityId: {SecurityId}, Q: {Quantity}, T: {Time:yyyyMMdd-HHmmss}";
    }

    public virtual bool Equals(AssetState? obj)
    {
        return obj != null && Id == obj.Id && CompareTo(obj) == 0;
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(Id,
                                SecurityId,
                                AccountId,
                                Time,
                                Quantity);
    }

    public virtual int CompareTo(AssetState? other)
    {
        if (other == null) return 1;
        var r = AccountId.CompareTo(other.AccountId);
        if (r == 0) r = Time.CompareTo(other.Time);
        if (r == 0) r = SecurityId.CompareTo(other.SecurityId);
        if (r == 0) r = Quantity.CompareTo(other.Quantity);
        return r;
    }

    public virtual bool EqualsIgnoreId(IIdEntry other)
    {
        return other is AssetState asset && CompareTo(asset) == 0;
    }
}
