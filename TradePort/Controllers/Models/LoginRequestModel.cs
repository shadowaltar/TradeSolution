using Microsoft.AspNetCore.Mvc;
using System.ComponentModel;
using TradeCommon.Constants;
using TradeCommon.Runtime;
using RequiredAttribute = System.ComponentModel.DataAnnotations.RequiredAttribute;

namespace TradePort.Controllers.Models;

public class LoginRequestModel
{
    /// <summary>
    /// Admin password.
    /// </summary>
    [FromForm(Name = "admin-password")]
    [Required]
    public string AdminPassword { get; set; }

    /// <summary>
    /// User name.
    /// </summary>
    [FromForm(Name = "user")]
    [Required, DefaultValue("test")]
    public string UserName { get; set; } = "test";

    /// <summary>
    /// User password.
    /// </summary>
    [FromForm(Name = "user-password")]
    [Required, DefaultValue("testtest")]
    public string Password { get; set; } = "testtest";

    /// <summary>
    /// Account name; must be owned by given user.
    /// </summary>
    [FromForm(Name = "account-name")]
    [Required, DefaultValue("spot")]
    public string AccountName { get; set; } = "spot";

    /// <summary>
    /// Login environment.
    /// </summary>
    [FromForm(Name = "environment")]
    [Required, DefaultValue(EnvironmentType.Uat)]
    public EnvironmentType Environment { get; set; } = EnvironmentType.Uat;

    /// <summary>
    /// Connectivity to external system (exchange).
    /// </summary>
    [FromForm(Name = "exchange")]
    [Required, DefaultValue(ExchangeType.Binance)]
    public ExchangeType Exchange { get; set; } = ExchangeType.Binance;
}