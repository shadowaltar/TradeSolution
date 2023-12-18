using Common;
using Common.Attributes;
using TradeCommon.Database;
using TradeCommon.Runtime;

namespace TradeCommon.Essentials.Accounts;

/// <summary>
/// Account represents a record of entries under a user
/// on either client or broker/exchange side.
/// </summary>
[Storage(DatabaseNames.AccountTable, DatabaseNames.StaticData)]
[Unique(nameof(Name))]
public class Account : ITimeRelatedEntry
{
    /// <summary>
    /// Unique account id.
    /// </summary>
    [AutoIncrementOnInsert]
    public int Id { get; set; } = 0;

    /// <summary>
    /// Name of account; it must be stored in UPPER-CASE internally.
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
    /// Fee structure of this account. Some brokers may have different levels of feed structure.
    /// </summary>
    public string? FeeStructure { get; set; }

    public DateTime CreateTime { get; set; }

    public DateTime UpdateTime { get; set; }

    public override string ToString()
    {
        return $"[{Id}] {Name}, Owner: {OwnerId}, Type: {Type}, External: {ExternalAccount}";
    }

    public override bool Equals(object? obj)
    {
        return obj is Account account
               && Id == account.Id
               && Name == account.Name
               && OwnerId == account.OwnerId
               && ExternalAccount == account.ExternalAccount
               && BrokerId == account.BrokerId
               && Type == account.Type
               && (SubType == account.SubType || Conditions.BothBlank(SubType, account.SubType))
               && (FeeStructure == account.FeeStructure || Conditions.BothBlank(FeeStructure, account.FeeStructure))
               && CreateTime == account.CreateTime
               && UpdateTime == account.UpdateTime;
    }

    public override int GetHashCode()
    {
        var hc = new HashCode();
        hc.Add(Id);
        hc.Add(Name);
        hc.Add(OwnerId);
        hc.Add(ExternalAccount);
        hc.Add(BrokerId);
        hc.Add(Type);
        hc.Add(SubType);
        hc.Add(FeeStructure);
        hc.Add(CreateTime);
        hc.Add(UpdateTime);
        return hc.ToHashCode();
    }
}
