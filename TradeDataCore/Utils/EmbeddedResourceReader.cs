using log4net;
using System.Reflection;

namespace TradeDataCore.Utils;
public static class EmbeddedResourceReader
{
    private static readonly ILog Log = LogManager.GetLogger(typeof(EmbeddedResourceReader));

    public static StreamReader? GetStreamReader(string resourceName, string? suffix = "csv")
    {
        var assembly = Assembly.GetExecutingAssembly();
        try
        {
            Stream? stream = assembly.GetManifestResourceStream(resourceName);
            if (stream == null)
            {
                resourceName = $"{assembly.GetName().Name}.{resourceName}";
                if (!suffix.IsBlank())
                {
                    if (!resourceName.EndsWith("." + suffix, StringComparison.OrdinalIgnoreCase))
                        resourceName += "." + suffix;
                }
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