using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TradeCommon.Utils.Attributes;

namespace TradeCommon.Essentials.Portfolios;

/// <summary>
/// A balance entry in an account (one account may hold multiple balance entries).
/// </summary>
public class Balance
{
    public int AssetId { get; set; }

    /// <summary>
    /// Name of the asset, usually it is a kind of currency.
    /// </summary>
    [SelectIgnore]
    public string AssetName { get; set; }

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
}
