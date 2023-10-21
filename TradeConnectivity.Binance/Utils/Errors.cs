using Common;
using TradeCommon.Runtime;

namespace TradeConnectivity.Binance.Utils;

public class Errors
{
    public const string ClockOutOfSync = "Timestamp for this request was 1000ms ahead of the server's time.";

    public static ResultCode ProcessErrorMessage(string? errorMessage)
    {
        if (errorMessage.IsBlank()) return ResultCode.Ok;
        if (errorMessage.Contains(ClockOutOfSync))
        {
            return ResultCode.ClockOutOfSync;
        }
        return ResultCode.Ok;
    }
}
