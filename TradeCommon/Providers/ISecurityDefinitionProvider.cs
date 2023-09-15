using TradeCommon.Essentials.Instruments;

namespace TradeCommon.Providers;

public interface ISecurityDefinitionProvider
{
    Security GetSecurity(int securityId);
}