using System.Diagnostics;

namespace Common;

public static class Threads
{
    /// <summary>
    /// Put current thread to sleep and wait for <paramref name="trueCondition"/> returns true.
    /// If condition returns false, sleep for <paramref name="pollingMs"/> and recheck.
    /// After <paramref name="timeOut"/> ms, it will stop waiting and return.
    /// Returns true if condition is met and stop waiting.
    /// Returns false if timed out.
    /// </summary>
    /// <param name="trueCondition"></param>
    /// <param name="timeOut"></param>
    /// <param name="pollingMs"></param>
    public static bool WaitUntil(Func<bool> trueCondition, int timeOut = 60000, int pollingMs = 100)
    {
        var helper = new Helper(trueCondition, timeOut, pollingMs);
        return helper.WaitUntil();
    }

    private class Helper
    {
        private readonly Func<bool> _condition;
        private readonly int _timeOut;
        private readonly int _pollingMs;
        private readonly Stopwatch _stopWatch;

        public Helper(Func<bool> condition, int timeOut, int pollingMs)
        {
            _condition = condition;
            _timeOut = timeOut;
            _pollingMs = pollingMs;
            _stopWatch = new Stopwatch();
        }

        public bool WaitUntil()
        {
            _stopWatch.Start();
            while (true)
            {
                if (_condition.Invoke())
                    return true;
                if (_stopWatch.ElapsedMilliseconds > _timeOut)
                    return false;
                Thread.Sleep(_pollingMs);
            }
        }
    }
}
