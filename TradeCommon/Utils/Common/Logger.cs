using log4net;
using System.Diagnostics;

namespace Common;
public static class Logger
{
    public static ILog New()
    {
        var methodInfo = new StackTrace().GetFrame(1)?.GetMethod();
        var clz = methodInfo?.ReflectedType;
        if (clz == null)
            throw new InvalidOperationException("Unable to find the caller class.");
        return LogManager.GetLogger(clz);
    }
}
