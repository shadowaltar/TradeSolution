using System.ComponentModel.DataAnnotations;
using TradeCommon.Constants;
using Common.Attributes;

namespace TradeCommon.Essentials.Accounts;

[Unique(nameof(Name), nameof(Environment))]
public class User
{
    [UpsertIgnore, AutoIncrementOnInsert]
    [NotNegative]
    public int Id { get; set; } = -1;

    [Length(MinLength = 3)]
    public string Name { get; set; } = "";

    [Length(MinLength = 5), AlwaysLowerCase]
    public string Email { get; set; } = "";

    [NotUnknown]
    public string Environment { get; set; } = Environments.Test;

    public string EncryptedPassword { get; set; } = "";

    public DateTime CreateTime { get; set; }

    public DateTime UpdateTime { get; set; }

    [InsertIgnore, SelectIgnore]
    public List<Account> Accounts { get; } = new();
}
