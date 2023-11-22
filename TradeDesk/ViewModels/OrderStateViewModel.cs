
using System;
using TradeDesk.Services;

namespace TradeDesk.ViewModels;
public class OrderStateViewModel : AbstractViewModel
{
    private readonly Server _server;

    public OrderStateViewModel(Server server)
    {
        _server = server;
    }

    public void Initialize()
    {
    }
}
