using Common;
using TradeCommon.Utils.Common;

namespace TradeDataCore.StaticData;
public class Credential
{
    private const string HashedCredential = "15B397F2865224E4B7696F4A0176E143478F09C5206265E9FB5721F072AE96DB94963AA2F7C5635BAAC31ED1E2860B6B4FD8070D52F221F0FF73D161177D8ED3";
    private const string Salt = "special.trading.unicorn";

    public static bool IsPasswordCorrect(string password)
    {
        if (password.IsBlank()) return false;
        return CryptographyUtils.Encrypt(password, Salt) == HashedCredential;
    }
}
