using Common.Attributes;
using System.Diagnostics.CodeAnalysis;
using TradeCommon.Database;

namespace TradeCommon.Essentials.Accounts;

[Storage(DatabaseNames.UserTable, DatabaseNames.StaticData)]
[Unique(nameof(Name)), Unique(nameof(Email))]
public record User
{
    [AutoIncrementOnInsert]
    public int Id { get; set; } = 0;

    [NotNull, Length(MinLength = 3, MaxLength = 100)]
    public string Name { get; set; } = "";

    [NotNull, Length(MinLength = 5, MaxLength = 100), AlwaysLowerCase]
    public string Email { get; set; } = "";

    [NotNull, Length(MaxLength = 512)]
    public string EncryptedPassword { get; set; } = "";

    [NotNull]
    public DateTime CreateTime { get; set; }

    public DateTime UpdateTime { get; set; }

    [DatabaseIgnore]
    public List<Account> Accounts { get; } = new();

    [DatabaseIgnore]
    public string LoginSessionId { get; set; }

    public override string ToString()
    {
        return $"[{Id}] {Name} {Email}";
    }
}
