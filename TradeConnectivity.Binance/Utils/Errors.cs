using Common;
using TradeCommon.Runtime;

namespace TradeConnectivity.Binance.Utils;

public class Errors
{
    public const string ClockOutOfSync = "Timestamp for this request was 1000ms ahead of the server's time.";

    public static ResultCode ProcessErrorMessage(string? errorMessage)
    {
        return errorMessage.IsBlank() ? ResultCode.Ok : errorMessage.Contains(ClockOutOfSync) ? ResultCode.ClockOutOfSync : ResultCode.Ok;
    }
}
