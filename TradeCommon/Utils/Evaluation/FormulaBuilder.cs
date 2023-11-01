using org.mariuszgromada.math.mxparser;

namespace TradeCommon.Utils.Evaluation;


public class FormulaBuilder
{
    private Function _function;

    public static FormulaBuilder NewLogic(string formula)
    {
        var builder = new FormulaBuilder
        {
            _function = new Function(formula)
        };
        return builder;
    }

    public Formula Build()
    {
        return Compile();
    }

    public FormulaBuilder WithFunction(FormulaBuilder builder)
    {
        throw new NotImplementedException();
    }

    private Formula Compile()
    {
        throw new NotImplementedException();
    }
}