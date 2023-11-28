using Microsoft.AspNetCore.Mvc;
using TradeCommon.Runtime;

namespace TradePort.Controllers.Models;

public class UserCreationModel
{
    [FromForm(Name = "admin-password")]
    public string? AdminPassword { get; set; }

    [FromForm(Name = "userPassword")]
    public string? UserPassword { get; set; }

    [FromForm(Name = "email")]
    public string? Email { get; set; }

    [FromForm(Name = "environment")]
    public EnvironmentType Environment { get; set; }
}