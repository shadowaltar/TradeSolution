using Common.Attributes;
using TradeCommon.Database;
using TradeCommon.Runtime;
using TradeCommon.Utils.Common;

namespace TradeCommon.Essentials.Accounts;

/// <summary>
/// Account represents a record of entries under a user
/// on either client or broker/exchange side.
/// </summary>
[Storage(DatabaseNames.AccountTable, DatabaseNames.StaticData)]
[Unique(nameof(Name), nameof(Environment))]
public class Account : ITimeRelatedEntry
{
    /// <summary>
    /// Unique account id.
    /// </summary>
    [AutoIncrementOnInsert]
    public int Id { get; set; } = 0;

    /// <summary>
    /// Name of account;
    /// </summary>
    [NotBlank]
    public string Name { get; set; } = "";

    /// <summary>
    /// Account owner.
    /// </summary>
    [NotNegative]
    public int OwnerId { get; set; } = 0;

    /// <summary>
    /// External account id from broker.
    /// </summary>
    [NotBlank]
    public string ExternalAccount { get; set; } = "";

    /// <summary>
    /// Name of the broker.
    /// </summary>
    [Positive]
    public int BrokerId { get; set; } = 0;

    /// <summary>
    /// Type of the account, for example it indicates if it is a sport or margin trading one.
    /// </summary>
    [AlwaysUpperCase, NotBlank]
    public string? Type { get; set; }

    /// <summary>
    /// Sub-type of the account. Some brokers may contain multiple levels of types.
    /// </summary>
    [AlwaysUpperCase(IsNullOk = true)]
    public string? SubType { get; set; }

    /// <summary>
    /// Trading environment like production or paper trading.
    /// </summary>
    [NotUnknown]
    public EnvironmentType Environment { get; set; }

    /// <summary>
    /// Fee structure of this account. Some brokers may have different levels of feed structure.
    /// </summary>
    public string? FeeStructure { get; set; }

    public DateTime CreateTime { get; set; }

    public DateTime UpdateTime { get; set; }

    public override string ToString()
    {
        return $"[{Id}] {Name}, Owner: {OwnerId}, Env: {Environment}, Type: {Type}, External: {ExternalAccount}";
    }

    public override bool Equals(object? obj)
    {
        if (obj is not Account account) return false;

        var r = Id == account.Id &&
               Name == account.Name &&
               OwnerId == account.OwnerId &&
               ExternalAccount == account.ExternalAccount &&
               BrokerId == account.BrokerId &&
               Type == account.Type &&
               (SubType == account.SubType || Conditions.BothBlank(SubType, account.SubType)) &&
               Environment == account.Environment &&
               (FeeStructure == account.FeeStructure || Conditions.BothBlank(FeeStructure, account.FeeStructure)) &&
               CreateTime == account.CreateTime &&
               UpdateTime == account.UpdateTime;

        return false;
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(Id,
            Name,
            OwnerId,
            ExternalAccount,
            BrokerId,
            Type,
            SubType,
            HashCode.Combine(
                Environment,
                FeeStructure,
                CreateTime,
                UpdateTime));
    }
}
