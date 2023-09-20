namespace Common.Database;

public interface IDatabaseSchemaHelper
{
    string CreateInsertSql<T>(char placeholderPrefix, bool isUpsert, string? tableNameOverride = null);

    string CreateDropTableAndIndexSql<T>(string? tableNameOverride = null);

    string CreateCreateTableAndIndexSql<T>(string? tableNameOverride = null);

    string GetCreateTableUniqueClause<T>();

    string GetCreateTableUniqueIndexStatement<T>(string tableName);

    string GetDropTableUniqueIndexStatement<T>(string tableName);
}