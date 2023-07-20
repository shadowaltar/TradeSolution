using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TradeCommon.Essentials;

public class User
{
    public int Id { get; set; }
    
    public string Name { get; set; }
    
    public string? ExternalId { get; set; }
    
    public List<Account> Accounts { get; set; }
}
