using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TradeDataCore.Database
{
    public class Storage
    {
        public static async Task SaveStaticData<T>(string table, List<T> entries)
        {
            using var connection = new SqliteConnection("Data Source=" + DatabaseFileNames.StaticData);
            await connection.OpenAsync();

            var command = connection.CreateCommand();
            command.CommandText = @$"SELECT * FROM {table}";
            using var reader = command.ExecuteReader();
            while (await reader.ReadAsync())
            {
                var name = reader.GetString(0);

                Console.WriteLine($"Hello, {name}!");
            }
            await connection.CloseAsync();
        }
    }
}
