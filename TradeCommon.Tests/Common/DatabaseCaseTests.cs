using Common;
using Common.Attributes;
using Common.Database;
using Microsoft.Data.Sqlite;
using Moq;
using TradeCommon.Database;

namespace TradeLogicCore.Tests;

[TestFixture()]
public class DatabaseCaseTests
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

        var dbMock = new Mock<IDatabase>();
        dbMock.Setup(x => x.SqlHelper).Returns(new SqliteSqlBuilder());
        var db = dbMock.Object;
        var sqlWriter = new SqlWriter<TestObject>(db, "test");
        string dropSql = db.SqlHelper.CreateDropTableAndIndexSql<TestObject>();
        string createSql = db.SqlHelper.CreateCreateTableAndIndexSql<TestObject>();

        using var conn = new SqliteConnection(sqlWriter.ConnectionString);
        await conn.OpenAsync();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = dropSql;
        await cmd.ExecuteNonQueryAsync(); 
        cmd.CommandText = createSql;
        await cmd.ExecuteNonQueryAsync(); // re-create table

        var sql = sqlBuilder.CreateUpsertSql<TestObject>(placeholderPrefix);
        var objects = new List<TestObject> { object1, objectIdConflict, objectExIdConflict };
        cmd.CommandText = sql;

        // upsert one by one
        foreach (var @object in objects)
        {
            var result = await sqlWriter.UpsertOne(@object);

            cmd.CommandText = "SELECT Id, ExternalId, Result FROM " + sqlWriter.DefaultTableName;
            using var reader = cmd.ExecuteReader();
            Assert.That(reader.Read(), Is.True);
            Assert.That(reader[0], Is.EqualTo(@object.Id));
            Assert.That(reader[1], Is.EqualTo(@object.ExternalId));
            Assert.That(reader[2], Is.EqualTo(@object.Result));
            Assert.That(reader.Read(), Is.False);
        }

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