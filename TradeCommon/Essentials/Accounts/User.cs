using TradeCommon.Constants;
using TradeCommon.Runtime;
using TradeCommon.Utils.Attributes;

namespace TradeCommon.Essentials.Accounts;

[Unique(nameof(Name), nameof(Environment))]
public class User
{
    [UpsertIgnore]
    [AutoIncrementOnInsert]
    public int Id { get; set; } = -1;

    public string Name { get; set; } = "";

    public string Email { get; set; } = "";

    public string Environment { get; set; } = Environments.Test;

    public string EncryptedPassword { get; set; } = "";

    public DateTime CreateTime { get; set; }

    public DateTime UpdateTime { get; set; }

    [InsertIgnore]
    [SelectIgnore]
    public List<Account> Accounts { get; } = new();
}
