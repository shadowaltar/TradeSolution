using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using TradeApp.Services;
using TradeApp.ViewModels.Widgets;

namespace TradeApp.Demo
{
    public static class DemoInitializer
    {
        internal static void Start(MainViewModel mainViewModel)
        {
        }

        public static void Initialize(DepthViewModel depthViewModel)
        {
            var ds = new DemoDepthDataService();
            depthViewModel.Reset();
            ds.Initialize(depthViewModel.DepthCount);
            depthViewModel.Connect(ds);
        }
    }

    public class DemoDepthDataService : IDepthDataService
    {
        private Timer? _timer;
        private Random _r;
        private int _maxDepthCount;

        public event DepthUpdateDelegate DepthUpdated;
        public event AllDepthsUpdateDelegate AllDepthsUpdated;

        public DemoDepthDataService()
        {
            _r = new Random((int)DateTime.Now.Ticks);
        }

        public void Dispose()
        {
            _timer?.Dispose();
        }

        public double[] Initialize(int maxDepthCount)
        {
            _maxDepthCount = maxDepthCount;
            var prices = GeneratePrices();
            Start();
            return prices;
        }

        private double[] GeneratePrices()
        {
            var mid = Convert.ToDouble(_r.Next(50, 100));
            var deviations = new List<double>();
            var prices = new double[_maxDepthCount * 2];
            var c = _maxDepthCount;
            for (int i = 0; i < c; i++)
            {
                deviations.Add(Math.Round((_r.NextDouble() + 0.2d) * 2d, 2));
            }
            var devSum = deviations.Sum();
            prices[0] = mid + devSum;
            for (int i = 1; i < c; i++)
            {
                prices[i] = prices[i - 1] - deviations[i - 1];
            }

            deviations.Clear();
            for (int i = 0; i < c; i++)
            {
                deviations.Add(Math.Round((_r.NextDouble() + 0.2d) * 2d, 2));
            }
            prices[c] = mid - deviations[0];
            for (int i = 1; i < c; i++)
            {
                prices[c + i] = prices[c] - deviations[i];
            }
            return prices;
        }

        private void Start()
        {
            _timer = new Timer(YieldPriceChange);
            _timer.Change(0, 300);
        }

        private void YieldPriceChange(object? state)
        {
            AllDepthsUpdated?.Invoke("",GeneratePrices());
        }
    }
}
