using System.Net;

namespace TradeCommon.Utils;
public static class Firewall
{
    public static bool CanCall { get; } = !Dns.GetHostName().Contains("PAG");
}
