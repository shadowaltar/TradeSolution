using TradeCommon.Constants;

namespace Common.Database;

public interface IDatabaseSchemaHelper
{
    string CreateInsertSql<T>(char placeholderPrefix, bool isUpsert, string? tableNameOverride = null) where T : class;

    string CreateDropTableAndIndexSql<T>(string? tableNameOverride = null) where T : class;

    string CreateCreateTableAndIndexSql<T>(string? tableNameOverride = null) where T : class;

    string CreateDeleteSql<T>(char placeholderPrefix = Consts.SqlCommandPlaceholderPrefix,
                              string? tableNameOverride = null) where T : class;

    string GetCreateTableUniqueClause<T>();

    string GetCreateTableUniqueIndexStatement<T>(string tableName);

    string GetDropTableUniqueIndexStatement<T>(string tableName);
}