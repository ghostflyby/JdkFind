using System.Text;

namespace JdkFind.Cli;

internal static class TableFormatter
{
    internal static void Write(IReadOnlyList<Jvm> jvms, TextWriter writer)
    {
        var headers = new[] { "VERSION", "VENDOR", "ARCH", "SOURCE", "HOME" };
        var rows = jvms.Select(jvm => new[]
        {
            jvm.Version.Original,
            jvm.Vendor ?? "-",
            jvm.Architecture ?? "-",
            string.Join('+', jvm.Providers),
            jvm.Home.FullName,
        }).ToArray();

        var widths = new int[headers.Length];
        for (var column = 0; column < headers.Length; column++)
        {
            widths[column] = headers[column].Length;
            foreach (var row in rows)
                widths[column] = Math.Max(widths[column], row[column].Length);
        }

        writer.WriteLine(Format(headers, widths));
        foreach (var row in rows)
            writer.WriteLine(Format(row, widths));
    }

    private static string Format(string[] cells, int[] widths)
    {
        var builder = new StringBuilder();
        for (var column = 0; column < cells.Length; column++)
        {
            if (column > 0)
                builder.Append("  ");

            // The last column is not padded, avoiding trailing whitespace.
            builder.Append(column == cells.Length - 1 ? cells[column] : cells[column].PadRight(widths[column]));
        }

        return builder.ToString();
    }
}
