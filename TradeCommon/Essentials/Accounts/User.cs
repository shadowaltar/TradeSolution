using TradeCommon.Constants;
using TradeCommon.Runtime;
using TradeCommon.Utils.Attributes;

namespace TradeCommon.Essentials.Accounts;

public class User
{
    [InsertIgnore]
    public int Id { get; set; } = -1;

    [UpsertConflictKey]
    public string Name { get; set; } = "";

    public string Email { get; set; } = "";

    [UpsertConflictKey]
    public string Environment { get; set; } = Environments.Test;

    public string EncryptedPassword { get; set; } = "";

    public DateTime CreateTime { get; set; }

    public DateTime UpdateTime { get; set; }

    [InsertIgnore]
    [SelectIgnore]
    public List<Account> Accounts { get; } = new();
}
