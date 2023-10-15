namespace TradeDataCore.Essentials;

public enum OperatorType
{
    Unknown,
    Plus,
    Minus,
    Multiply,
    Divide,

    Equals,
    NotEquals,
    LargerThan,
    LargerThanOrEquals,
    SmallerThan,
    SmallerThanOrEquals,
}

public static class OperatorTypeConverter
{
    public static OperatorType Parse(string value)
    {
        return value switch
        {
            "+" => OperatorType.Plus,
            "-" => OperatorType.Minus,
            "*" => OperatorType.Multiply,
            "/" or "\\" => OperatorType.Divide,
            "=" or "==" => OperatorType.Equals,
            "!=" or "<>" => OperatorType.NotEquals,
            ">" => OperatorType.LargerThan,
            ">=" => OperatorType.LargerThanOrEquals,
            "<" => OperatorType.SmallerThan,
            "<=" => OperatorType.SmallerThanOrEquals,
            _ => OperatorType.Unknown,
        };
    }

    public static string ConvertToSqlString(this OperatorType type)
    {
        return type switch
        {
            OperatorType.Unknown => "?",
            OperatorType.Plus => "+",
            OperatorType.Minus => "-",
            OperatorType.Multiply => "*",
            OperatorType.Divide => "/",
            OperatorType.Equals => "=",
            OperatorType.NotEquals => "<>",
            OperatorType.LargerThan => ">",
            OperatorType.SmallerThan => "<",
            OperatorType.LargerThanOrEquals => ">=",
            OperatorType.SmallerThanOrEquals => "<=",
            _ => throw new NotImplementedException(),
        };
    }

    public static string ConvertToCodeString(this OperatorType type)
    {
        return type switch
        {
            OperatorType.Unknown => "?",
            OperatorType.Plus => "+",
            OperatorType.Minus => "-",
            OperatorType.Multiply => "*",
            OperatorType.Divide => "/",
            OperatorType.Equals => "==",
            OperatorType.NotEquals => "!=",
            OperatorType.LargerThan => ">",
            OperatorType.SmallerThan => "<",
            OperatorType.LargerThanOrEquals => ">=",
            OperatorType.SmallerThanOrEquals => "<=",
            _ => throw new NotImplementedException(),
        };
    }
}