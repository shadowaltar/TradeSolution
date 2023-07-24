using Common;
using log4net;
using System.Reflection;

namespace TradeCommon.Utils;
public static class EmbeddedResourceReader
{
    private static readonly ILog _log = Logger.New();

    public static StreamReader? GetStreamReader(string resourceName, string? suffix = "csv")
    {
        var assembilies = AppDomain.CurrentDomain.GetAssemblies().ToDictionary(a=>a.GetName().Name, a=>a);
        foreach (var (_, assembly) in assembilies.Where(p=> !p.Key.StartsWith("System") && !p.Key.StartsWith("Microsoft")))
        {
            var name = resourceName;
            try
            {
                Stream? stream = assembly.GetManifestResourceStream(name);
                if (stream == null)
                {
                    name = $"{assembly.GetName().Name}.{name}";
                    if (!suffix.IsBlank())
                    {
                        if (!name.EndsWith("." + suffix, StringComparison.OrdinalIgnoreCase))
                            name += "." + suffix;
                    }
                }
                stream = assembly.GetManifestResourceStream(name);
                if (stream == null)
                    continue;

                var reader = new StreamReader(stream);
                return reader;
            }
            catch (Exception ex)
            {
                _log.Error("Failed to read resource: " + name, ex);
                return null;
            }
        }
        return null;
    }
}