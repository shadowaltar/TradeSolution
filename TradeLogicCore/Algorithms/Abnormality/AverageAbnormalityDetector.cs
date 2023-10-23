namespace TradeLogicCore.Algorithms.Abnormality;

public class AverageAbnormalityDetector
{
    private readonly LinkedList<decimal> _values = new();
    public int Count { get; }
    public decimal LastAverage { get; private set; }
    public decimal Average { get; private set; }
    public decimal ThresholdPercentage { get; private set; }

    public AverageAbnormalityDetector(decimal thresholdPercentage, int itemCount)
    {
        ThresholdPercentage = thresholdPercentage;
        Count = itemCount;
    }

    public bool IsNormal(decimal value)
    {
        lock (_values)
        {
            _values.AddLast(value);
            if (_values.Count > Count)
                _values.RemoveFirst();

            Average = _values.Average();
            if (LastAverage == 0 && Average != 0)
                LastAverage = Average;
            var diff = LastAverage - value;
            var pct = Math.Abs(LastAverage == 0 ? 0 : diff / LastAverage);
            LastAverage = Average;
            return pct < ThresholdPercentage;
        }
    }
}
