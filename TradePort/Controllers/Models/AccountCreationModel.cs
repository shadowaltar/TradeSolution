using Microsoft.AspNetCore.Mvc;
using TradeCommon.Constants;
using TradeCommon.Runtime;

namespace TradePort.Controllers.Models;

public class AccountCreationModel
{
    [FromForm(Name = "admin-password")]
    public string? AdminPassword { get; set; }

    [FromForm(Name = "ownerName")]
    public string? OwnerName { get; set; }

    [FromForm(Name = "brokerType")]
    public BrokerType Broker { get; set; }

    [FromForm(Name = "externalAccount")]
    public string? ExternalAccount { get; set; }

    [FromForm(Name = "type")]
    public string? Type { get; set; }

    [FromForm(Name = "subType")]
    public string? SubType { get; set; }

    [FromForm(Name = "feeStructure")]
    public string? FeeStructure { get; set; }
}
