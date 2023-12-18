using Common.Database;

namespace TradeCommon.Database;

public interface IDatabase
{
    event Action<object, string> Success;

    event Action<object, Exception, string> Failed;

    long GetMax(string fieldName, string tableName, string databaseName);
    
    IDatabaseSqlBuilder SqlHelper { get; }
}