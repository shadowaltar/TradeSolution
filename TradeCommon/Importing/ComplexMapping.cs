namespace TradeCommon.Importing;

public class ComplexMapping
{
    public Func<object, object> Function { get; }
    public string ParameterFieldName { get; }

    public ComplexMapping(Func<object, object> f, string parameterFieldName)
    {
        Function = f;
        ParameterFieldName = parameterFieldName;
    }
}
