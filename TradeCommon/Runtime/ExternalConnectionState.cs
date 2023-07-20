using CsvHelper.Configuration.Attributes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TradeCommon.Runtime;

public class ExternalConnectionState
{
    public ConnectionActionType Type { get; set; }
    public string? StatusCode { get; set; }
    public string? ExternalPartyId { get; set; }
    public string? UniqueConnectionId { get; set; }
    public string? Description { get; set; }
}

public enum ConnectionActionType
{
    Unknown,
    Connect,
    Disconnect,
}