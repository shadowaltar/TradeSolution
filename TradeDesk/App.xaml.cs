using Autofac;
using Common;
using log4net.Config;
using System.Text;
using System.Windows;
using TradeCommon.Constants;
using TradeDesk.Services;
using TradeDesk.Utils;
using TradeDesk.ViewModels;
using TradeDesk.Views;

namespace TradeDesk;
/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    public App()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        XmlConfigurator.Configure();
        Logger.ApplyConfig();

        Syncfusion.Licensing.SyncfusionLicenseProvider.RegisterLicense("Ngo9BigBOggjHTQxAR8/V1NHaF1cWmhIfEx1RHxQdld5ZFRHallYTnNWUj0eQnxTdEZiW35acnZWQ2NaUE12Vg==");

        var builder = new ContainerBuilder();
        Ui.RegisterViewAndViewModels(builder);
        builder.RegisterSingleton<Server>();

        var container = builder.Build();
        var (lv, lvm) = container.ResolveAndSetDataContext<LoginView, LoginViewModel>();
        lvm.AfterLogin += OnLoggedIn;
        lv.Show();

        void OnLoggedIn(string token)
        {
            lvm.AfterLogin -= OnLoggedIn;
            var (mv, mvm) = container.ResolveAndSetDataContext<MainView, MainViewModel>();

            var session = new ClientSession(lvm.UserName,
                                            lvm.Account,
                                            lvm.EnvironmentType,
                                            lvm.ExchangeType,
                                            ExternalNames.Convert(lvm.ExchangeType),
                                            token);
            mvm.Initialize(lvm.ServerUrlWithPort, session);
            Ui.Invoke(mv.Show);
            Ui.BeginInvoke(lv.Close);
        }
    }
}
