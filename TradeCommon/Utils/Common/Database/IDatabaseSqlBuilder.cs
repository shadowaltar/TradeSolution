﻿namespace Common.Database;

public interface IDatabaseSqlBuilder
{
    string CreateUpsertSql<T>(char placeholderPrefix, string? tableNameOverride = null) where T : class;

    string CreateInsertSql<T>(char placeholderPrefix, string? tableNameOverride = null) where T : class;

    string CreateDropTableAndIndexSql<T>(string? tableNameOverride = null) where T : class;

    string CreateCreateTableAndIndexSql<T>(string? tableNameOverride = null) where T : class;

    string CreateDeleteSql<T>(char placeholderPrefix = '$',
                              string? tableNameOverride = null) where T : class;

    string GetCreateTableUniqueClause<T>();

    string GetCreateTableUniqueIndexStatement<T>(string tableName);

    string GetDropTableUniqueIndexStatement<T>(string tableName);
}