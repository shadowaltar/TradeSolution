using CsvHelper;
using System.Globalization;

namespace Common;
public static class Csv
{
    public static Dictionary<string, TV?> ReadAsDictionary<TV>(string fileName, Func<string[], TV>? converter = null) where TV : class
    {
        using var reader = new StreamReader(fileName);
        using var csvReader = new CsvReader(reader, CultureInfo.InvariantCulture);
        var results = new Dictionary<string, TV?>();
        csvReader.Read();
        csvReader.ReadHeader();
        while (csvReader.Read())
        {
            var p = csvReader.GetRecord<dynamic>() as IDictionary<string, object>;
            if (p == null)
                continue;

            if (p.Count < 2) // must be at least two columns
                continue;

            string? key = null;
            TV? value = null;
            if (p.Count == 2 || converter == null)
            {
                foreach (var kv in p)
                {
                    if (key == null)
                    {
                        key = (string)kv.Value;
                    }
                    else
                    {
                        value = (converter == null ? kv.Value : converter.Invoke(new string[] { (string)kv.Value })) as TV;
                        // if column count > 2 (value column count > 1) and no converter, just use the 1st value column's values
                        break;
                    }
                }
            }
            else if (p.Count > 2 && converter != null)
            {
                var values = new string[p.Count - 1];
                var i = 0;
                foreach (var kv in p)
                {
                    if (key == null)
                    {
                        key = (string)kv.Value;
                    }
                    else
                    {
                        values[i] = (string)kv.Value;
                        i++;
                    }
                }

                value = converter.Invoke(values) as TV;
            }
            if (key != null)
            {
                results.Add(key, value);
            }
        }
        return results;
    }

    public static Dictionary<string, T> Read<T>(string fileName, Func<T, string> keySelector)
    {
        // key is Id, value is CustodianAccountSetting
        var records = new Dictionary<string, T>();
        using var reader = new StreamReader(fileName);
        using var csvReader = new CsvReader(reader, CultureInfo.InvariantCulture);
        csvReader.Read();
        csvReader.ReadHeader();
        while (csvReader.Read())
        {
            var record = csvReader.GetRecord<T>();
            if (record == null)
                continue;
            records[keySelector.Invoke(record)] = record;
        }
        return records;
    }

    public static List<T> Read<T>(string fileName)
    {
        // key is Id, value is CustodianAccountSetting
        var records = new List<T>();
        using var reader = new StreamReader(fileName);
        using var csvReader = new CsvReader(reader, CultureInfo.InvariantCulture);
        csvReader.Read();
        csvReader.ReadHeader();
        while (csvReader.Read())
        {
            var record = csvReader.GetRecord<T>();
            if (record == null)
                continue;
            records.Add(record);
        }
        return records;
    }
}