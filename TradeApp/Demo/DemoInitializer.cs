using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using TradeApp.Essentials;
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

        public DepthLevel[] Initialize(int maxDepthCount)
        {
            _maxDepthCount = maxDepthCount;
            var prices = GeneratePrices();
            Start();
            return prices;
        }

        private DepthLevel[] GeneratePrices()
        {
            int RandomVolume() => _r.Next(10000, 2000);

            var count = _maxDepthCount;
            var mid = Convert.ToDouble(_r.Next(50, 100));
            var deviations = new List<double>();
            var prices = new DepthLevel[count * 2];
            for (int i = 0; i < count; i++)
            {
                deviations.Add(Math.Round((_r.NextDouble() + 0.2d) * 2d, 2));
            }
            // asks
            var devSum = deviations.Sum();
            prices[0] = new DepthLevel(count, mid + devSum, RandomVolume(), BidAsk.Ask);
            for (int i = 1; i < count; i++)
            {
                var last = prices[i - 1];
                prices[i] = new DepthLevel(last.Depth - 1,
                    last.Price - deviations[i - 1],
                    RandomVolume(), BidAsk.Ask);
            }

            // bids
            deviations.Clear();
            for (int i = 0; i < count; i++)
            {
                deviations.Add(Math.Round((_r.NextDouble() + 0.2d) * 2d, 2));
            }
            prices[count] = new DepthLevel(1, mid - deviations[0], RandomVolume(), BidAsk.Bid);
            for (int i = 1; i < count; i++)
            {
                var last = prices[count + i - 1];
                prices[count + i] = new DepthLevel(last.Depth + 1,
                    last.Price - deviations[i],
                    RandomVolume(), BidAsk.Bid);
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
            AllDepthsUpdated?.Invoke("", GeneratePrices());
        }
    }
}
