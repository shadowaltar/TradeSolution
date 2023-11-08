using Common;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Windows.Input;
using TradeCommon.Constants;
using TradeCommon.Runtime;
using TradeDesk.Utils;

namespace TradeDesk.ViewModels;
public class LoginViewModel : AbstractViewModel
{
    private string _account;
    private ExchangeType _exchangeType;
    private string _serverUrl;
    private string _userName;
    private EnvironmentType _environmentType;

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
        var url = $"{ServerUrl.Trim('/')}/{RestApiConstants.AdminRoot}/{RestApiConstants.Login}";

        using var client = new HttpClient();
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
        var result = await response.Content.ReadAsStringAsync();
        if (Enum.Parse<ResultCode>(result) == ResultCode.LoginUserAndAccountOk)
        {
            MessageBoxes.Info(null, "Login successful!", "Login");
        }
    }
}
