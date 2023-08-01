using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TradeCommon.Essentials.Portfolios;

namespace TradeCommon.Essentials;

/// <summary>
/// Account represents a record of entries under a user
/// on either client or broker/exchange side.
/// </summary>
public class Account
{
    /// <summary>
    /// Unique account id.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Unique account id from broker.
    /// </summary>
    public string BrokerAccountId { get; set; }

    /// <summary>
    /// Account owner.
    /// </summary>
    public int UserId { get; set; }

    /// <summary>
    /// Name of the broker.
    /// </summary>
    public string BrokerName { get; set; }

    /// <summary>
    /// Type of the account, for example it indicates if it is a sport or margin trading one.
    /// </summary>
    public string Type { get; set; }

    /// <summary>
    /// Subt-type of the account. Some brokers may contain multiple levels of types.
    /// </summary>
    public string SubType { get; set; }

    /// <summary>
    /// Trading environment like production or paper trading.
    /// </summary>
    public string TradingEnvironment { get; set; }

    /// <summary>
    /// Fee structure of this account. Some brokers may have different levels of feed structure.
    /// </summary>
    public string FeeStructure { get; set; }

    public List<Balance> Balances { get; } = new();
}
