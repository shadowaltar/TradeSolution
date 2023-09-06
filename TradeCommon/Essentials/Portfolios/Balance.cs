using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Common.Attributes;

namespace TradeCommon.Essentials.Portfolios;

/// <summary>
/// A balance entry in an account (one account may hold multiple balance entries).
/// </summary>
/// 
[Unique(nameof(AssetId), nameof(AccountId))]
public class Balance
{
    public int AssetId { get; set; }

    /// <summary>
    /// The associated account's Id.
    /// </summary>
    public int AccountId { get; set; }

    /// <summary>
    /// Name of the asset, usually it is a kind of currency.
    /// </summary>
    [SelectIgnore, InsertIgnore, UpsertIgnore]
    public string AssetCode { get; set; } = "";

    /// <summary>
    /// Amount of asset being held which is free to use.
    /// </summary>
    public decimal FreeAmount { get; set; }

    /// <summary>
    /// Amount of asset being held which is locked by broker.
    /// </summary>
    public decimal LockedAmount { get; set; }

    /// <summary>
    /// Amount of asset being held which is in settlement process.
    /// </summary>
    public decimal SettlingAmount { get; set; }

    public DateTime UpdateTime { get; set; }

    public override string ToString()
    {
        return $"[{AssetId}] {AssetCode}, {FreeAmount}/{LockedAmount}, {UpdateTime:yyyyMMdd-HHmmss}";
    }

    public override bool Equals(object? obj)
    {
        return obj is Balance balance &&
               AssetId == balance.AssetId &&
               AssetCode == balance.AssetCode &&
               FreeAmount == balance.FreeAmount &&
               LockedAmount == balance.LockedAmount &&
               SettlingAmount == balance.SettlingAmount &&
               UpdateTime == balance.UpdateTime;
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(AssetId, AssetCode, FreeAmount, LockedAmount, SettlingAmount, UpdateTime);
    }
}
