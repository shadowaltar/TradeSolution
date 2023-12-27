using log4net;
using log4net.Appender;
using log4net.Config;
using System.Configuration;
using System.Diagnostics;

namespace Common;
public static class Logger
{
    public static ILog Global { get; } = LogManager.GetLogger("Global");

    public static ILog New()
    {
        var methodInfo = new StackTrace().GetFrame(1)?.GetMethod();
        var clz = methodInfo?.ReflectedType;
        return clz == null ? throw new InvalidOperationException("Unable to find the caller class.") : LogManager.GetLogger(clz);
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

    public static void ApplyConfig()
    {
        XmlConfigurator.Configure(new FileInfo(ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None).FilePath));
    }
}
