using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TradeDesk.ViewModels;
internal class NewOrderViewModel : AbstractViewModel
{
    public OrderViewModel Parent { get; internal set; }
}
