using Microsoft.AspNetCore.Mvc;
using System.ComponentModel;
using RequiredAttribute = System.ComponentModel.DataAnnotations.RequiredAttribute;

namespace TradePort.Controllers.Models;

public class LoginRequestModel
{
    /// <summary>
    /// Admin password. Required by PROD only.
    /// </summary>
    [FromForm(Name = "admin-password")]
    public string? AdminPassword { get; set; }

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
}