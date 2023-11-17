using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http.Features;

namespace TradePort.Utils;

public static class HttpContextExtensions
{
    public static bool IsSessionAvailable(this HttpContext httpContext)
    {
        var sessionFeature = httpContext.Features.Get<ISessionFeature>();
        return sessionFeature != null;
    }

    public static bool IsAuthenticationAvailable(this HttpContext httpContext)
    {
        var authenticateResultFeature = httpContext.Features.Get<IAuthenticationFeature>();
        return authenticateResultFeature != null;
    }
}
