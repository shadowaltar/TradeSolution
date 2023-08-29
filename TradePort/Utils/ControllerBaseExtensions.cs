using Common;
using Microsoft.AspNetCore.Mvc;
using TradeCommon.Essentials.Instruments;
using TradeCommon.Essentials;
using TradeDataCore.Essentials;
using TradeCommon.Runtime;
using TradeCommon.Constants;

namespace TradePort.Utils;

public static class ControllerBaseExtensions
{
    public static bool IsIntervalBad(this string? intervalStr, out IntervalType interval, out ObjectResult? result)
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

    public static bool IsSecurityTypeBad(this string? secTypeStr, out SecurityType securityType, out ObjectResult? result)
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

    public static bool IsTimeRangeBad(this string? rangeStr, out TimeRangeType timeRangeType, out ObjectResult? result)
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

    public static bool IsDateBad(this string? timeStr, out DateTime date, out ObjectResult? result)
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

    public static bool IsEnvBad(this string? envStr, out EnvironmentType env, out ObjectResult? result)
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

    public static bool IsExchangeBad(this string? exchangeStr, out ExchangeType exchange, out ObjectResult? result)
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
}
