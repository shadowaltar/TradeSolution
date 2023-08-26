using TradeCommon.Essentials.Portfolios;
using TradeCommon.Runtime;
using TradeCommon.Utils.Attributes;

namespace TradeCommon.Essentials.Accounts;

/// <summary>
/// Account represents a record of entries under a user
/// on either client or broker/exchange side.
/// </summary>
public class Account
{
    /// <summary>
    /// Unique account id.
    /// </summary>
    [InsertIgnore]
    public int Id { get; set; }

    /// <summary>
    /// Name of account;
    /// </summary>
    [UpsertConflictKey]
    public string Name { get; set; } = "";

    /// <summary>
    /// Account owner.
    /// </summary>
    public int OwnerId { get; set; }

    /// <summary>
    /// External account id from broker.
    /// </summary>
    public string ExternalAccount { get; set; }

    /// <summary>
    /// Name of the broker.
    /// </summary>
    public int BrokerId { get; set; }

    /// <summary>
    /// Type of the account, for example it indicates if it is a sport or margin trading one.
    /// </summary>
    public string? Type { get; set; }

    /// <summary>
    /// Sub-type of the account. Some brokers may contain multiple levels of types.
    /// </summary>
    public string? SubType { get; set; }

    /// <summary>
    /// Trading environment like production or paper trading.
    /// </summary>
    [UpsertConflictKey]
    public EnvironmentType Environment { get; set; }

    /// <summary>
    /// Fee structure of this account. Some brokers may have different levels of feed structure.
    /// </summary>
    public string? FeeStructure { get; set; }

    public DateTime CreateTime { get; set; }

    public DateTime UpdateTime { get; set; }

    [InsertIgnore]
    [UpsertIgnore]
    public Balance? MainBalance { get; set; }

    [InsertIgnore]
    [UpsertIgnore]
    public List<Balance> Balances { get; } = new();
}
