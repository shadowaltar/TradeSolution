using TradeCommon.Constants;
using TradeCommon.Runtime;

namespace TradePort.Controllers.Models;

public record LoginResponseModel(ResultCode Result,
                                     int UserId,
                                     string UserName,
                                     string Email,
                                     int AccountId,
                                     string AccountName,
                                     ExchangeType Exchange,
                                     BrokerType Broker,
                                     EnvironmentType Environment,
                                     DateTime SessionExpiry,
                                     string Token);