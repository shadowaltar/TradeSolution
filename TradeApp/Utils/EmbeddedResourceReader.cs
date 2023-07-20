using log4net;
using System;
using System.IO;
using System.Reflection;

namespace TradeApp.Utils;

public static class EmbeddedResourceReader
{
    private static readonly ILog _log = Common.Logger.New();

    public static StreamReader? GetStreamReader(string resourceName)
    {
        var assembly = Assembly.GetExecutingAssembly();
        try
        {
            Stream? stream = assembly.GetManifestResourceStream(resourceName);
            if (stream == null) return null;

            var reader = new StreamReader(stream);
            return reader;
        }
        catch (Exception ex)
        {
            _log.Error("Failed to read resource: " + resourceName, ex);
            return null;
        }
    }
}
