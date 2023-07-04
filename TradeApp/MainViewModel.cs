using Autofac;
using DevExpress.Mvvm;
using System.Windows.Input;
using TradeApp.Demo;
using TradeApp.UX;
using TradeApp.ViewModels;
using TradeApp.ViewModels.Widgets;

namespace TradeApp;
public class MainViewModel : AbstractViewModel
{
    private string _title;
    private IContainer _container;

    public MainView View { get; }
    public WorkspaceManager WorkspaceManager { get; internal set; }

    public string Title { get => _title; set => SetValue(ref _title, value); }

    public ICommand NewWindowCommand { get; }
    public ICommand NewPanelCommand { get; }
    public ICommand StartDemoCommand { get; }

    public MainViewModel(MainView view, WorkspaceManager workspaceManager)
    {
        Title = "Trading App";
        View = view;
        WorkspaceManager = workspaceManager;

        NewWindowCommand = new DelegateCommand(OpenWindow);
        NewPanelCommand = new DelegateCommand<PresetPanelType>(CreatePanel);
        StartDemoCommand = new DelegateCommand(StartDemo);
    }

    public void Initialize(IContainer container)
    {
        _container = container;
        WorkspaceManager.Initialize(View.DockLayoutManager, container);
    }

    private void OpenWindow()
    {
        var mv = new MainView();
        mv.Show();
    }

    private void CreatePanel(PresetPanelType type)
    {
        WorkspaceManager.LoadPanel(type);
    }

    private void StartDemo()
    {
        DemoInitializer.Start(this);
        foreach(var panelViewModel in WorkspaceManager.PanelViewModels.Values)
        {
            if (panelViewModel is DepthViewModel dvm)
            {
                DemoInitializer.Initialize(dvm);
            }
        }
    }
}