namespace TradeCommon.Calculations;

public abstract class Calculator
{
    public virtual int Period { get; protected set; }
    
    public string Label { get; protected set; } = "";


    public virtual double Next(double value)
    {
        return double.NaN;
    }

    public virtual decimal Next(decimal value)
    {
        return decimal.MinValue;
    }
}