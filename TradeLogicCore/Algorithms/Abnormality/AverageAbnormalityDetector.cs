namespace TradeLogicCore.Algorithms.Abnormality;

public class AverageAbnormalityDetector
{
    private readonly LinkedList<decimal> _values = new();
    public int Count { get; }
    public decimal LastAverage { get; private set; }
    public decimal Average { get; private set; }
    public decimal Threshold { get; private set; }

    public AverageAbnormalityDetector(int itemCount)
    {
        Count = itemCount;
    }

    public bool CheckAbnormality(decimal value)
    {
        lock (_values)
        {
            _values.AddLast(value);
            if (_values.Count > Count)
                _values.RemoveFirst();

            Average = _values.Average();
            var diff = LastAverage - value;
            LastAverage = Average;
            return Math.Abs(diff) > Threshold;
        }
    }
}
