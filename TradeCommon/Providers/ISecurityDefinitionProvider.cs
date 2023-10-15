using Common;
using TradeCommon.Essentials.Instruments;
using TradeCommon.Essentials.Portfolios;
using TradeCommon.Runtime;

namespace TradeCommon.Providers;

public interface ISecurityDefinitionProvider
{
    /// <summary>
    /// Gets a security by its code. It is possible to return null when not found.
    /// </summary>
    /// <param name="code"></param>
    /// <returns></returns>
    Security? GetSecurity(string? code);

    /// <summary>
    /// Gets a security by its unique id. Throws exception if not found.
    /// </summary>
    /// <param name="securityId"></param>
    /// <returns></returns>
    Security GetSecurity(int securityId);

    /// <summary>
    /// Fix any missing security info in given entry.
    /// </summary>
    /// <param name="entry"></param>
    /// <param name="security"></param>
    /// <returns></returns>
    void Fix(SecurityRelatedEntry entry, Security? security = null);
}