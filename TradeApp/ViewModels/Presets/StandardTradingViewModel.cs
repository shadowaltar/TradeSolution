using TradeApp.ViewModels.Widgets;

namespace TradeApp.ViewModels.Presets;


public class StandardTradingViewModel : AbstractViewModel
{
    private DepthViewModel _depthViewModel;

    public DepthViewModel DepthViewModel { get => _depthViewModel; set => SetValue(ref _depthViewModel, value); }

    public StandardTradingViewModel()
    {
        DepthViewModel = new DepthViewModel();
    }
}

