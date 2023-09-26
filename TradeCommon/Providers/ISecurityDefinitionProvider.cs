using TradeCommon.Essentials.Instruments;

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
}