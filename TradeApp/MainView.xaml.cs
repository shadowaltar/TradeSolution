using Autofac;
using DevExpress.Xpf.Core;
using DevExpress.Xpf.Docking;
using TradeApp.UX;
using TradeApp.ViewModels.Presets;
using TradeApp.ViewModels.Widgets;
using WorkspaceManager = TradeApp.UX.WorkspaceManager;

namespace TradeApp;
/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainView : ThemedWindow
{
    public MainViewModel ViewModel { get; private set; }
    public DockLayoutManager DockLayoutManager => MainArea;

    public MainView()
    {
        InitializeComponent();

        var builder = new ContainerBuilder();

        builder.RegisterType<MainViewModel>()
            .WithParameter(new TypedParameter(typeof(MainView), this))
            .WithParameter(new TypedParameter(typeof(WorkspaceManager), new WorkspaceManager()));
        builder.RegisterType<FavoriteViewModel>();
        builder.RegisterType<StandardTradingViewModel>();
        builder.RegisterType<CandlePriceViewModel>();
        builder.RegisterType<DepthViewModel>();
        builder.RegisterType<SimplePriceViewModel>();

        var container = builder.Build();

        ViewModel = container.Resolve<MainViewModel>();
        DataContext = ViewModel;
        ViewModel.Initialize(container);

        if (ViewModel.WorkspaceManager.LastWorkspace == null || !ViewModel.WorkspaceManager.ShallLoadLastWorkspaceOnInit)
        {
            ViewModel.WorkspaceManager.LoadPanel(PresetPanelType.Welcome);
        }
    }
}
