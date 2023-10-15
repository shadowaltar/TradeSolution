using log4net;
using TradeCommon.Essentials.Instruments;

namespace Common;
public static class Utils
{
    private static readonly ILog _log = Logger.New();

    public static T AutoRetry<T>(this Func<T> func, int retryCount = 5, int retryWaitMs = 1000, ILog? log = null)
    {
        if (retryCount < 0) throw new ArgumentException("Must specify a positive retry count.");
        if (retryWaitMs < 0) throw new ArgumentException("Must specify a positive retry waiting interval in ms.");
        log ??= _log;
        var count = 0;
        while (count < retryCount)
        {
            try
            {
                return func.Invoke();
            }
            catch (Exception e)
            {
                count++;
                if (count >= retryCount)
                {
                    log.Error($"Failed to execute; all retry failed.", e);
                    return default;
                }
                else
                {
                    log.Error($"Failed to execute, retrying {count}/{retryCount} after {retryWaitMs}ms...", e);
                    Thread.Sleep(retryWaitMs);
                }
            }
        }
        return default;
    }

    public static bool IsValid(this Security? security)
    {
        if (security == null) return false;
        if (!security.Id.IsValid() || security.Code.IsBlank()) return false;
        return true;
    }

    public static CompareReport ReportComparison<T>(T a, T b)
    {
        var vg = ReflectionUtils.GetValueGetter<T>();
        var aTuples = vg.GetNamesAndValues(a).ToList();
        var bTuples = vg.GetNamesAndValues(b).ToList();
        var report = new CompareReport(typeof(T));
        if (aTuples.Count != bTuples.Count)
        {
            report.InvalidCount = true;
            return report;
        }
        var results = new string[aTuples.Count];
        for (int i = 0; i < aTuples.Count; i++)
        {
            var aTuple = aTuples[i];
            var bTuple = bTuples[i];
            if (!Equals(aTuple.Item1, bTuple.Item1))
            {
                report.AddInvalid(aTuple.Item1, bTuple.Item1, aTuple.Item2, bTuple.Item2);
            }
            else
            {
                report.Add(aTuple.Item1, aTuple.Item2, bTuple.Item2);
            }
        }
        return report;
    }
}


public class CompareReport
{
    public Type Type { get; }
    public List<(string propertyName, bool isEqual, object value1, object value2)> Values { get; } = new();
    public bool InvalidCount { get; internal set; }
    public CompareReport(Type type)
    {
        Type = type;
    }

    public void AddInvalid(string propertyName1, string propertyName2, object value1, object value2)
    {
        Values.Add((propertyName1 + "|" + propertyName2, false, value1, value2));
    }

    public void Add(string propertyName, object value1, object value2)
    {
        Values.Add((propertyName, Equals(value1, value2), value1, value2));
    }
}