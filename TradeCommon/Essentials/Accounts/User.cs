using System.ComponentModel.DataAnnotations;
using TradeCommon.Constants;
using Common.Attributes;
using System.Diagnostics.CodeAnalysis;
using System.ComponentModel.DataAnnotations.Schema;
using TradeCommon.Database;

namespace TradeCommon.Essentials.Accounts;

[Unique(nameof(Name), nameof(Environment)), Unique(nameof(Email), nameof(Environment))]
[Storage(DatabaseNames.UserTable, null, DatabaseNames.StaticData)]
public class User
{
    [AutoIncrementOnInsert]
    public int Id { get; set; } = -1;

    [Length(MinLength = 3, MaxLength = 100)]
    public string Name { get; set; } = "";

    [Length(MinLength = 5, MaxLength = 100), AlwaysLowerCase]
    public string Email { get; set; } = "";

    [NotUnknown, NotNull]
    public string Environment { get; set; } = Environments.Unknown;

    [Length(MaxLength = 512), NotNull]
    public string EncryptedPassword { get; set; } = "";

    [NotNull]
    public DateTime CreateTime { get; set; }

    public DateTime UpdateTime { get; set; }

    [DatabaseIgnore]
    public List<Account> Accounts { get; } = new();

    public override string ToString()
    {
        return $"[{Id}] {Name}, {Environment}";
    }
}
