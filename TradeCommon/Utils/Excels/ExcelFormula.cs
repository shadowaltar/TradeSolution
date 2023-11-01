namespace Common.Excels;
public class ExcelFormula
{
    public string? FormulaR1C1 { get; init; }
    public string? FormulaA1 { get; init; }
    public bool IsArrayFormula { get; private set; }
    public string Formula => FormulaA1 ?? FormulaR1C1 ?? "";

    public static ExcelFormula NewR1C1(string formula, bool isArrayFormula = false)
    {
        var f = new ExcelFormula
        {
            FormulaR1C1 = formula,
            IsArrayFormula = isArrayFormula
        };
        return f;
    }

    public static ExcelFormula NewA1(string formula, bool isArrayFormula = false)
    {
        var f = new ExcelFormula
        {
            FormulaA1 = formula,
            IsArrayFormula = isArrayFormula
        };
        return f;
    }

    public override string ToString()
    {
        return IsArrayFormula ? $"ArrayFormula: {Formula}" : $"Formula: {Formula}";
    }
}
