using Microsoft.AspNetCore.Mvc;
using System.ComponentModel;
using TradeCommon.Runtime;
using RequiredAttribute = System.ComponentModel.DataAnnotations.RequiredAttribute;

namespace TradePort.Controllers.Models;

public class ChangeUserPasswordModel
{
    [Required, FromForm(Name = "admin-password")]
    public string? AdminPassword { get; set; }

    /// <summary>
    /// User name.
    /// </summary>
    [FromForm(Name = "user")]
    [Required, DefaultValue("test")]
    public string UserName { get; set; } = "test";

    /// <summary>
    /// New password.
    /// </summary>
    [Required, FromForm(Name = "new-password")]
    public string? NewPassword { get; set; }

    /// <summary>
    /// Environment of this user.
    /// </summary>
    [Required, FromForm(Name = "environment"), DefaultValue(EnvironmentType.Uat)]
    public EnvironmentType Environment { get; set; } = EnvironmentType.Uat;
}