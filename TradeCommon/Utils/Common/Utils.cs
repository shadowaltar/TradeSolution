using log4net;
using TradeCommon.Essentials.Instruments;

namespace Common;
public static class Utils
{
    private static readonly ILog _log = Logger.New();

    public static T AutoRetry<T>(this Func<T> func, int retryCount = 5, int retryWaitMs = 1000, ILog? log = null)
    {
        if (retryCount < 0) throw new ArgumentException("Must specify a positive retry count.");
        if (retryWaitMs < 0) throw new ArgumentException("Must specify a positive retry waiting interval in ms.");
        log ??= _log;
        var count = 0;
        while (count < retryCount)
        {
            try
            {
                return func.Invoke();
            }
            catch (Exception e)
            {
                count++;
                if (count >= retryCount)
                {
                    log.Error($"Failed to execute; all retry failed.", e);
                    return default;
                }
                else
                {
                    log.Error($"Failed to execute, retrying {count}/{retryCount} after {retryWaitMs}ms...", e);
                    Thread.Sleep(retryWaitMs);
                }
            }
        }
        return default;
    }

    public static bool IsValid(this Security? security)
    {
        if (security == null) return false;
        if (!security.Id.IsValid() || security.Code.IsBlank()) return false;
        return true;
    }
}
