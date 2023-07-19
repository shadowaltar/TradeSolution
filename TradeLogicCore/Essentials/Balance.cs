using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TradeLogicCore.Essentials;

/// <summary>
/// A balance entry in an account (one account may hold multiple balance entries).
/// </summary>
public class Balance
{
    /// <summary>
    /// Name of the asset, usually it is a kind of currency.
    /// </summary>
    public string AssetName { get; set; }

    /// <summary>
    /// Amount of asset being held which is free to use.
    /// </summary>
    public decimal FreeAmount { get; set; }

    /// <summary>
    /// Amount of asset being held which is in settlement process.
    /// </summary>
    public decimal SettlingAmount { get; set; }

    /// <summary>
    /// Amount of asset being held which is locked by broker.
    /// </summary>
    public decimal LockedAmount { get; set; }
}
