using Common;
using TradeCommon.Essentials.Accounts;

namespace TradeDataCore.StaticData;
public class Credential
{
    private const string HashedCredential = "15B397F2865224E4B7696F4A0176E143478F09C5206265E9FB5721F072AE96DB94963AA2F7C5635BAAC31ED1E2860B6B4FD8070D52F221F0FF73D161177D8ED3";
    private const string PasswordSalt = "special.trading.unicorn";

    public static bool Matches(string input, string hash)
    {
        return !input.IsBlank() && !hash.IsBlank() && CryptographyUtils.Encrypt(input, PasswordSalt) == hash;
    }

    public static bool IsPasswordCorrect(User user, string password)
    {
        if (password.IsBlank()) return false;
        if (user == null) throw new ArgumentNullException(nameof(user));
        var encryptedPassword =
            CryptographyUtils.Encrypt(user.Name + password, PasswordSalt)
            + CryptographyUtils.Encrypt(user.Email.ToLowerInvariant() + password, PasswordSalt)
            + CryptographyUtils.Encrypt(user.Environment.ToUpperInvariant() + password, PasswordSalt);
        return encryptedPassword == user.EncryptedPassword;
    }

    public static bool IsAdminPasswordCorrect(string password)
    {
        return !password.IsBlank() && CryptographyUtils.Encrypt(password, PasswordSalt) == HashedCredential;
    }

    public static void EncryptUserPassword(User user, ref string password)
    {
        if (user == null) throw new ArgumentNullException(nameof(user));
        if (user.Name.IsBlank()) throw new ArgumentNullException(nameof(user.Name));
        if (password.IsBlank()) throw new ArgumentNullException(nameof(password));

        var encrypted1 = CryptographyUtils.Encrypt(user.Name + password, PasswordSalt);
        var encrypted2 = CryptographyUtils.Encrypt(user.Email.ToLowerInvariant() + password, PasswordSalt);
        var encrypted3 = CryptographyUtils.Encrypt(user.Environment.ToUpperInvariant() + password, PasswordSalt);
        user.EncryptedPassword = encrypted1 + encrypted2 + encrypted3;

        // erase the original one
        password = "";
    }
}
