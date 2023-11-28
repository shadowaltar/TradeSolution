using Common;
using Microsoft.AspNetCore.Mvc;
using TradeCommon.Essentials;
using TradeDataCore.Instruments;
using TradeDataCore.MarketData;

namespace TradePort.Controllers;

public class StreamingQuotationController : ControllerBase
{
    [Route("/stream/ohlc/{signature}")]
    public async Task<IActionResult> SubscribeOhlcPrices([FromServices] DataPublisher publisher,
        [FromServices] ISecurityService securityService,
        [FromRoute(Name = "signature")] string signature)
    {
        var (securityCode, interval) = ParseSignature(signature);
        if (interval == IntervalType.Unknown)
            return BadRequest("Invalid interval.");
        var security = securityService.GetSecurity(securityCode);
        if (security == null)
            return BadRequest("Invalid security.");

        if (HttpContext.WebSockets.IsWebSocketRequest)
        {
            using var webSocket = await HttpContext.WebSockets.AcceptWebSocketAsync();
            await publisher.PublishOhlc(webSocket, security, interval);
        }
        else
        {
            HttpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
        }
        return Ok($"Subscribed to OHLC prices for {securityCode} @ {interval}");
    }

    private (string securityCode, IntervalType interval) ParseSignature(string signature)
    {
        if (signature.IsBlank())
            return ("", IntervalType.Unknown);

        var parts = signature.Split('_');
        if (parts.Length != 2)
            return ("", IntervalType.Unknown);

        return (parts[0], IntervalTypeConverter.Parse(parts[1]));
    }
}
