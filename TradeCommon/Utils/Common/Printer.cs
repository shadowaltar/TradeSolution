using System.Text;

namespace Common;

public class Printer
{
    public static string Print<T>(List<List<T>> data)
    {
        var sb = new StringBuilder();
        foreach (var list in data)
        {
            foreach (var item in list)
            {
                sb.Append(item).Append(" | ");
            }
            sb.Remove(sb.Length - 3, 3);
            sb.AppendLine();
        }
        return sb.ToString();
    }

    public static string Print<T>(List<T> data)
    {
        var sb = new StringBuilder().AppendLine("----------------");
        for (int i = 0; i < data.Count; i++)
        {
            T? item = data[i];
            sb.Append(item);
            if (i != data.Count - 1)
            {
                sb.Append(" | ");
            }
        }
        sb.Append("----------------");
        return sb.ToString();
    }
}
