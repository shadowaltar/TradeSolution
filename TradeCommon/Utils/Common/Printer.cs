using System.Text;

namespace Common;

public class Printer
{
    public static string Print<T>(List<List<T>> data)
    {
        var sb = new StringBuilder().AppendLine("----------------");
        foreach (var list in data)
        {
            foreach (var item in list)
            {
                sb.Append(item).Append(" | ");
            }
            sb.Remove(sb.Length - 3, 3);
            sb.AppendLine();
        }
        sb.Append("----------------");
        return sb.ToString();
    }
}
