using Common;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Input;
using TradeCommon.Constants;
using TradeCommon.Runtime;
using TradeDesk.Utils;
using TradeDesk.Views;

namespace TradeDesk.ViewModels;
public class LoginViewModel : AbstractViewModel
{
    private string _account;
    private ExchangeType _exchangeType;
    private string _serverUrl;
    private string _userName;
    private EnvironmentType _environmentType;

    public event Action<bool> AfterLogin;

    public LoginView Window { get; internal set; }

    public ObservableCollection<EnvironmentType> EnvironmentTypes { get; } = new();

    public ObservableCollection<ExchangeType> ExchangeTypes { get; } = new();

    public string Account { get => _account; set => SetValue(ref _account, value); }

    public EnvironmentType EnvironmentType { get => _environmentType; set => SetValue(ref _environmentType, value); }

    public ExchangeType ExchangeType { get => _exchangeType; set => SetValue(ref _exchangeType, value); }

    public string ServerUrl { get => _serverUrl; set => SetValue(ref _serverUrl, value); }

    public string UserName { get => _userName; set => SetValue(ref _userName, value); }

    public string UserPassword { get; set; }

    public string AdminPassword { get; set; }

    public ICommand LoginCommand { get; }

    public LoginViewModel()
    {
        LoginCommand = new DelegateCommand(Login);
        ((IList<EnvironmentType>)EnvironmentTypes).AddRange(Enum.GetValues<EnvironmentType>());
        ((IList<ExchangeType>)ExchangeTypes).AddRange(Enum.GetValues<ExchangeType>());

        UserName = "test";
        UserPassword = "testtest";
        Account = "spot";
        ServerUrl = "https://localhost:7065";
        EnvironmentType = EnvironmentType.Uat;
        ExchangeType = ExchangeType.Binance;
    }

    private async void Login()
    {
        AfterLogin?.Invoke(true);
        return;

        var url = $"{ServerUrl.Trim('/')}/{RestApiConstants.AdminRoot}/{RestApiConstants.Login}";
        try
        {
            using var client = HttpHelper.HttpClientWithoutCert();
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            var request = new HttpRequestMessage
            {
                Method = HttpMethod.Post,
                RequestUri = new Uri(url)
            };
            var content = new MultipartFormDataContent
            {
                { new StringContent(UserName), "user" },
                { new StringContent(UserPassword), "user-password" },
                { new StringContent(Account), "account-name" },
                { new StringContent(EnvironmentType.ToString()), "environment" },
                { new StringContent(ExchangeType.ToString()), "exchange" },
                { new StringContent(AdminPassword), "admin-password" }
            };
            request.Content = content;
            var header = new ContentDispositionHeaderValue("form-data");
            request.Content.Headers.ContentDisposition = header;

            var response = await client.PostAsync(request.RequestUri.ToString(), request.Content);
            if (response.IsSuccessStatusCode)
            {
                var loginContent = await response.Content.ReadFromJsonAsync<JsonElement>();

                var resultCodeStr = loginContent.GetProperty("result").GetString();
                if (!Enum.TryParse<ResultCode>(resultCodeStr, out var resultCode))
                {
                    MessageBoxes.Info(null, "Result: " + resultCode, "Login Failed");
                }
                var token = loginContent.GetProperty("Token").GetString();
                // must set the auth-token from now on
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            }
        }
        catch (Exception e)
        {
            MessageBoxes.Info(null, "Error: " + e.Message, "Login Failed");
        }
    }
}
