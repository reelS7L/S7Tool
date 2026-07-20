using System.IO;
using System.Text;

namespace S7Tool.Helpers;

public static class CsvExporter
{
    public static void Export(string filePath, IEnumerable<string> headers, IEnumerable<IEnumerable<string?>> rows)
    {
        var sb = new StringBuilder();
        sb.AppendLine(string.Join(";", headers.Select(Escape)));

        foreach (var row in rows)
            sb.AppendLine(string.Join(";", row.Select(Escape)));

        File.WriteAllText(filePath, sb.ToString(), Encoding.UTF8);
    }

    private static string Escape(string? value)
    {
        value ??= "";
        return value.Contains(';') || value.Contains('"') || value.Contains('\n')
            ? "\"" + value.Replace("\"", "\"\"") + "\""
            : value;
    }
}
