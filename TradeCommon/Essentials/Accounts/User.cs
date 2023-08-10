using System.Security.Permissions;
using TradeCommon.Utils.Attributes;

namespace TradeCommon.Essentials.Accounts;

public class User
{
    [InsertIgnore]
    public int Id { get; set; } = -1;

    public string Name { get; set; } = "";

    public string Email { get; set; } = "";

    public string EncryptedPassword { get; set; } = "";

    public DateTime CreateTime { get; set; }

    public DateTime UpdateTime { get; set; }

    [InsertIgnore]
    [SelectIgnore]
    public List<Account> Accounts { get; } = new();
}
