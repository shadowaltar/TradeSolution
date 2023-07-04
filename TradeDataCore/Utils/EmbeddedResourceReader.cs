using log4net;
using System.Reflection;

namespace TradeDataCore.Utils;
public static class EmbeddedResourceReader
{
    private static readonly ILog Log = LogManager.GetLogger(typeof(EmbeddedResourceReader));

    public static StreamReader? GetStreamReader(string resourceName)
    {
        var assembly = Assembly.GetExecutingAssembly();
        try
        {
            Stream? stream = assembly.GetManifestResourceStream(resourceName);
            if (stream == null)
            {
                resourceName = $"{assembly.GetName().Name}.{resourceName}";
                if (!resourceName.EndsWith(".csv", StringComparison.OrdinalIgnoreCase))
                    resourceName += ".csv";
            }
            stream = assembly.GetManifestResourceStream(resourceName);
            if (stream == null)
                return null;

            var reader = new StreamReader(stream);
            return reader;
        }
        catch (Exception ex)
        {
            Log.Error("Failed to read resource: " + resourceName, ex);
            return null;
        }
    }
}