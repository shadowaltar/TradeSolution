using Common;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using TradeCommon.Constants;
using TradeCommon.Essentials;
using TradeCommon.Essentials.Instruments;
using TradeCommon.Runtime;
using TradeDataCore.Essentials;
using TradeDataCore.StaticData;

namespace TradePort.Utils;

public static class ControllerValidator
{
    public static bool IsAdminPasswordBad(string? password, EnvironmentType environment, [NotNullWhen(true)] out ObjectResult? result)
    {
        result = null;
        if (password.IsBlank() || !Credential.IsAdminPasswordCorrect(password, environment))
        {
            result = new BadRequestObjectResult("Invalid admin password.");
            return true;
        }
        return false;
    }

    public static bool IsIntNegative(int i, [NotNullWhen(true)] out ObjectResult? result, [CallerArgumentExpression(nameof(i))] string inputName = "")
    {
        result = null;
        if (i < 0)
        {
            result = new BadRequestObjectResult($"Invalid {inputName}. Must >= 0.");
            return true;
        }
        return false;
    }

    public static bool IsDecimalNegative(decimal i, [NotNullWhen(true)] out ObjectResult? result, [CallerArgumentExpression(nameof(i))] string inputName = "")
    {
        result = null;
        if (i < 0)
        {
            result = new BadRequestObjectResult($"Invalid {inputName}. Must >= 0.");
            return true;
        }
        return false;
    }

    public static bool IsIntNegativeOrZero(int i, [NotNullWhen(true)] out ObjectResult? result, [CallerArgumentExpression(nameof(i))] string inputName = "")
    {
        result = null;
        if (i <= 0)
        {
            result = new BadRequestObjectResult($"Invalid {inputName}. Must > 0.");
            return true;
        }
        return false;
    }

    public static bool IsDecimalNegativeOrZero(decimal i, [NotNullWhen(true)] out ObjectResult? result, [CallerArgumentExpression(nameof(i))] string inputName = "")
    {
        result = null;
        if (i <= 0)
        {
            result = new BadRequestObjectResult($"Invalid {inputName}. Must > 0.");
            return true;
        }
        return false;
    }

    public static bool IsStringTooShort(string? str, int len, [NotNullWhen(true)] out ObjectResult? result, [CallerArgumentExpression(nameof(str))] string inputName = "")
    {
        if (len < 0) throw new ArgumentException("Invalid string length.", nameof(len));

        result = null;
        if (str.IsBlank() || str.Length < len)
        {
            result = new BadRequestObjectResult($"Invalid {inputName}. Must be at least of length {len}.");
            return true;
        }
        return false;
    }

    public static bool IsUnknown<T>(T val, [NotNullWhen(true)] out ObjectResult? result, [CallerArgumentExpression(nameof(val))] string inputName = "") where T : Enum
    {
        result = null;
        var intVal = Unsafe.As<T, int>(ref val);
        if (intVal == 0)
        {
            result = new BadRequestObjectResult($"Enum of type {typeof(T).Name} should not be {val}");
            return true;
        }
        return false;
    }

    public static bool IsBadOrParse(string? intervalStr, out IntervalType interval, [NotNullWhen(true)] out ObjectResult? result)
    {
        result = null;
        interval = IntervalTypeConverter.Parse(intervalStr);
        if (interval == IntervalType.Unknown)
        {
            result = new BadRequestObjectResult("Invalid interval string.");
            return true;
        }
        return false;
    }

    public static bool IsBadOrParse(string? secTypeStr, out SecurityType securityType, [NotNullWhen(true)] out ObjectResult? result)
    {
        result = null;
        securityType = SecurityTypeConverter.Parse(secTypeStr);
        if (securityType == SecurityType.Unknown)
        {
            result = new BadRequestObjectResult("Invalid sec-type string.");
            return true;
        }
        return false;
    }

    public static bool IsBadOrParse(string? rangeStr, out TimeRangeType timeRangeType, [NotNullWhen(true)] out ObjectResult? result)
    {
        result = null;
        timeRangeType = TimeRangeTypeConverter.Parse(rangeStr);
        if (timeRangeType == TimeRangeType.Unknown)
        {
            result = new BadRequestObjectResult("Invalid time range string.");
            return true;
        }
        return false;
    }

    public static bool IsBadOrParse(string? timeStr, out DateTime date, [NotNullWhen(true)] out ObjectResult? result)
    {
        result = null;
        date = timeStr.ParseDate();
        if (date == DateTime.MinValue)
        {
            result = new BadRequestObjectResult("Invalid start date-time.");
            return true;
        }
        return false;
    }

    public static bool IsBadOrParse(string? envStr, out EnvironmentType env, [NotNullWhen(true)] out ObjectResult? result)
    {
        result = null;
        env = TradeCommon.Constants.Environments.Parse(envStr);
        if (env == EnvironmentType.Unknown)
        {
            result = new BadRequestObjectResult("Invalid environment type date-time.");
            return true;
        }
        return false;
    }

    public static bool IsBadOrParse(string? exchangeStr, out ExchangeType exchange, [NotNullWhen(true)] out ObjectResult? result)
    {
        result = null;
        exchange = ExchangeTypeConverter.Parse(exchangeStr);
        if (exchange == ExchangeType.Unknown)
        {
            result = new BadRequestObjectResult("Invalid exchange type.");
            return true;
        }
        return false;
    }

    public static bool IsBadOrParse<T>(string? enumStr, T defaultInvalidValue, out T enumValue, [NotNullWhen(true)] out ObjectResult? result, [CallerArgumentExpression(nameof(enumStr))] string inputName = "") where T : Enum
    {
        result = null;
        if (enumStr.IsBlank())
        {
            enumValue = defaultInvalidValue;
            result = new BadRequestObjectResult($"Invalid {inputName} string.");
            return true;
        }

        enumValue = enumStr.ConvertDescriptionToEnum<T>();
        if (enumValue.Equals(defaultInvalidValue))
        {
            enumValue = defaultInvalidValue;
            result = new BadRequestObjectResult($"Invalid {inputName} string.");
        }
        return false;
    }

    public static bool IsBadOrParse(string? guidStr, out Guid guid, [NotNullWhen(true)] out ObjectResult? result)
    {
        result = null;
        guid = Guid.Empty;
        if (guidStr.IsBlank() || !Guid.TryParse(guidStr, out guid))
        {
            result = new BadRequestObjectResult("Invalid GUID.");
            return true;
        }
        return false;
    }
}
