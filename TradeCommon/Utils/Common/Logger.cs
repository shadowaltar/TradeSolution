using log4net;
using log4net.Appender;
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

    /// <summary>
    /// Get the log4net appender.
    /// Usage Example:
    /// var appender = (LogEventAppender)Logger.GetAppender("LogEventAppender")!;
    /// appender.NextLog -= OnLogged;
    /// appender.NextLog += OnLogged;
    /// </summary>
    /// <param name="name"></param>
    /// <returns></returns>
    public static IAppender? GetAppender(string name)
    {
        var appenders = LogManager.GetRepository().GetAppenders();
        return appenders.FirstOrDefault(a => a.Name == name);
    }
}
