namespace Common.Attributes;

[AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
public class StorageAttribute : Attribute
{
    public StorageAttribute(string tableName, string schemaName, string databaseName)
    {
        TableName = tableName;
        SchemaName = schemaName;
        DatabaseName = databaseName;
    }

    public StorageAttribute(string tableName, string databaseName)
    {
        TableName = tableName;
        SchemaName = null;
        DatabaseName = databaseName;
    }

    public string TableName { get; }
    public string? SchemaName { get; }
    public string DatabaseName { get; }

    public bool SortProperties { get; set; } = true;
}
