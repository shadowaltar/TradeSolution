using Common;

namespace TradeDataCore.StaticData;
public class Credential
{
    public static bool IsPasswordCorrect(string password)
    {
        if (password.IsBlank()) return false;
        if (password == Properties.Resources.AdminSecret)
            return true;
        return false;
    }
}
