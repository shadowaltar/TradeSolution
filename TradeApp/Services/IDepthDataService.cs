using System;
using TradeApp.Essentials;

namespace TradeApp.Services;
public interface IDepthDataService : IDisposable
{
    event DepthUpdateDelegate DepthUpdated;
    event AllDepthsUpdateDelegate AllDepthsUpdated;
}

public delegate void DepthUpdateDelegate(string ticker, DepthItem item);
public delegate void AllDepthsUpdateDelegate(string ticker, DepthItem[] items);
