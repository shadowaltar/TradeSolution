using Autofac;
using log4net.Config;
using System;
using System.Text;
using System.Windows.Input;
using TradeCommon.Constants;
using TradeCommon.Database;
using TradeCommon.Runtime;
using TradeDataCore.Instruments;
using TradeDesk.Services;
using TradeDesk.Utils;
using TradeDesk.Views;
using TradeLogicCore;
using TradeLogicCore.Services;

namespace TradeDesk.ViewModels;
public class MainViewModel : AbstractViewModel
{
    private readonly Server _server;
    private string _url;
    private readonly DelegateCommand connect;

    public string ServerUrl { get => _url; set => SetValue(ref _url, value); }

    public ClientSession? Session { get; private set; }
    public ICommand Connect { get; }


    public OverviewViewModel OverviewViewModel { get;  }
    public OrderViewModel OpenOrderViewModel { get;  }
    public OrderViewModel ErrorOrderViewModel { get;  }
    public OrderViewModel CancelledOrderViewModel { get;  }
    public OrderStateViewModel OrderStateViewModel { get;  }
    public TradeViewModel TradeViewModel { get; }
    public MainView Window { get; private set; }

    public MainViewModel()
    {
        _server = new Server();
        OpenOrderViewModel = new OrderViewModel(_server);
        ErrorOrderViewModel = new OrderViewModel(_server);
        CancelledOrderViewModel = new OrderViewModel(_server);
        OrderStateViewModel = new OrderStateViewModel(_server);

        TradeViewModel = new TradeViewModel(_server);

        OverviewViewModel = new OverviewViewModel(_server);

        Connect = new DelegateCommand(PerformConnect);
    }

    public void Initialize(MainView mainView)
    {
        Window = mainView;

        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        XmlConfigurator.Configure();


        Window.Hide();

        var loginView = new LoginView();
        var loginViewModel = new LoginViewModel();
        loginView.DataContext = loginViewModel;
        loginViewModel.AfterLogin += OnLoggedIn;
        loginView.ShowDialog();

        void OnLoggedIn(string token)
        {
            loginViewModel.AfterLogin -= OnLoggedIn;
            loginView.Close();
            ServerUrl = loginViewModel.ServerUrl;
            Session = new(loginViewModel.UserName,
                          loginViewModel.Account,
                          loginViewModel.EnvironmentType,
                          loginViewModel.ExchangeType,
                          ExternalNames.Convert(loginViewModel.ExchangeType),
                          token);
            Window.Show();
        }
    }

    private void PerformConnect()
    {
        OverviewViewModel.Initialize();
        OpenOrderViewModel.Initialize();
        OrderStateViewModel.Initialize();
        CancelledOrderViewModel.Initialize();
        ErrorOrderViewModel.Initialize();
        TradeViewModel.Initialize();
    }

    internal void Initialize(bool obj)
    {

    }
}
