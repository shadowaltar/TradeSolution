using Common.Attributes;
using TradeCommon.Essentials.Portfolios;
using TradeCommon.Runtime;
using TradeCommon.Utils.Common;

namespace TradeCommon.Essentials.Accounts;

/// <summary>
/// Account represents a record of entries under a user
/// on either client or broker/exchange side.
/// </summary>
[Storage("accounts", "static")]
[Unique(nameof(Name), nameof(Environment))]
public class Account
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

    [DatabaseIgnore]
    public Balance? MainBalance { get; private set; }

    [DatabaseIgnore]
    public List<Balance> Balances { get; } = new();

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
               UpdateTime == account.UpdateTime &&
               EqualityComparer<Balance?>.Default.Equals(MainBalance, account.MainBalance);
        if (r)
        {
            if (account.Balances.Count != Balances.Count) return false;
            // now counts are equal, assuming asset name of balance is already sorted
            for (int i = 0; i < account.Balances.Count; i++)
            {
                var a1 = Balances[i];
                var a2 = account.Balances[i];
                if (!a1.Equals(a2)) return false;
            }
            return true;
        }
        return false;
    }

    public override int GetHashCode()
    {
        HashCode hash = new HashCode();
        hash.Add(Id);
        hash.Add(Name);
        hash.Add(OwnerId);
        hash.Add(ExternalAccount);
        hash.Add(BrokerId);
        hash.Add(Type);
        hash.Add(SubType);
        hash.Add(Environment);
        hash.Add(FeeStructure);
        hash.Add(CreateTime);
        hash.Add(UpdateTime);
        hash.Add(MainBalance);
        return hash.ToHashCode();
    }
}
