using System;
using TradeDesk.Services;

namespace TradeDesk.ViewModels;
public class TradeViewModel : AbstractViewModel
{
    private Server _server;

    public TradeViewModel(Server server)
    {
        _server = server;
    }

    internal void Initialize()
    {
        throw new NotImplementedException();
    }
}
