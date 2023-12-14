using Common;
using Common.Attributes;
using Common.Database;
using Microsoft.Data.Sqlite;
using NUnit.Framework;

namespace TradeLogicCore.Tests;

[TestFixture()]
public class AdhocTests
{
    [Test()]
    public async Task MultiUniqueAttrSqliteUpsertTest()
    {
        var placeholderPrefix = '$';
        var object1 = new TestObject { Id = 1, ExternalId = 2, Result = "test" };
        var objectIdConflict = new TestObject { Id = 1, ExternalId = 3, Result = "test" };
        var objectExIdConflict = new TestObject { Id = 2, ExternalId = 3, Result = "test" };

        var sqlBuilder = new SqliteSqlBuilder();
        var valueGetter = ReflectionUtils.GetValueGetter<TestObject>();

        using var conn = new SqliteConnection("Data Source=C:\\TEMP\\test.db");
        await conn.OpenAsync();
        using var cmd = conn.CreateCommand();

        cmd.CommandText = "SELECT sqlite_version()";
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            Console.Out.WriteLine(reader[0]);
        }
        reader.Close();

        var createSql = sqlBuilder.CreateCreateTableAndIndexSql<TestObject>();
        cmd.CommandText = createSql;
        await cmd.ExecuteNonQueryAsync();

        var sql = sqlBuilder.CreateInsertSql<TestObject>(placeholderPrefix);
        var objects = new List<TestObject> { object1, objectIdConflict, objectExIdConflict };
        cmd.CommandText = sql;

        //        var t1 = @"INSERT INTO Tests
        //            (Id,ExternalId,Result)
        //VALUES
        //(1,2,'tt1')
        //ON CONFLICT (Id) DO UPDATE SET ExternalId = excluded.ExternalId,Result = excluded.Result
        //ON CONFLICT (ExternalId) DO UPDATE SET Id = excluded.Id,Result = excluded.Result
        //ON CONFLICT (Id, ExternalId) DO UPDATE SET Result = excluded.Result";
        //        cmd.CommandText = t1;
        //        Console.WriteLine(await cmd.ExecuteNonQueryAsync());

        //        var t2 = @"INSERT INTO Tests
        //            (Id,ExternalId,Result)
        //VALUES
        //(2,2,'tt2')
        //ON CONFLICT (Id) DO UPDATE SET ExternalId = excluded.ExternalId,Result = excluded.Result
        //ON CONFLICT (ExternalId) DO UPDATE SET Id = excluded.Id,Result = excluded.Result
        //ON CONFLICT (Id, ExternalId) DO UPDATE SET Result = excluded.Result";
        //        cmd.CommandText = t2;
        //        Console.WriteLine(await cmd.ExecuteNonQueryAsync());

        //        var t3 = @"INSERT INTO Tests
        //            (Id,ExternalId,Result)
        //VALUES
        //(2,3,'tt3')
        //ON CONFLICT (Id) DO UPDATE SET ExternalId = excluded.ExternalId,Result = excluded.Result
        //ON CONFLICT (ExternalId) DO UPDATE SET Id = excluded.Id,Result = excluded.Result
        //ON CONFLICT (Id, ExternalId) DO UPDATE SET Result = excluded.Result";
        //        cmd.CommandText = t3;
        //        Console.WriteLine(await cmd.ExecuteNonQueryAsync());



        foreach (var obj in objects)
        {
            Console.Out.WriteLine("----IN ----");
            cmd.Parameters.Clear();
            foreach (var (name, value) in valueGetter.GetNamesAndValues(obj))
            {
                Console.Out.Write(value + ",");
                cmd.Parameters.Add(new SqliteParameter(placeholderPrefix + name, value));
            }
            Console.Out.WriteLine();
            Console.Out.WriteLine(await cmd.ExecuteNonQueryAsync());
            Console.Out.WriteLine("----END----");
            Console.Out.WriteLine("----OUT----");
            cmd.CommandText = "SELECT * FROM Tests";
            using var r2 = cmd.ExecuteReader();
            while (r2.Read())
            {
                Console.Out.Write(r2[0] + ",");
                Console.Out.Write(r2[1] + ",");
                Console.Out.WriteLine(r2[2]);
            }
            Console.Out.WriteLine("----END----");
            r2.Close();
        }

        await conn.CloseAsync();
    }

    [Storage("Tests", "")]
    [Unique(nameof(Id))]
    [Unique(nameof(ExternalId))]
    public record TestObject
    {
        public long Id { get; set; }
        public long ExternalId { get; set; }
        public string Result { get; set; }
    }
}