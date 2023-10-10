using System.Diagnostics;

namespace Common;

public static class Threads
{
    public static void WaitUntil(Func<bool> condition, int timeOut = 60000, int pollingMs = 100)
    {
        var helper = new Helper(condition, timeOut, pollingMs);
        helper.WaitUntil();
    }

    private class Helper
    {
        private Func<bool> _condition;
        private int _timeOut;
        private int _pollingMs;
        private Stopwatch _stopWatch;

        public Helper(Func<bool> condition, int timeOut, int pollingMs)
        {
            _condition = condition;
            _timeOut = timeOut;
            _pollingMs = pollingMs;
            _stopWatch = new Stopwatch();
        }

        public void WaitUntil()
        {
            _stopWatch.Start();
            while (!_condition.Invoke() && _stopWatch.ElapsedMilliseconds <= _timeOut)
            {
                Thread.Sleep(_pollingMs);
            }
        }
    }
}
