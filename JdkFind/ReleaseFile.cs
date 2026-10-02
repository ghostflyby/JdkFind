using System.Globalization;
using System.Text;

namespace JdkFind;

/// <summary>
///     Parses the JEP 223 <c>release</c> file (<c>KEY="value"</c> lines) and derives
///     comparable version numbers from <c>JAVA_VERSION</c>.
/// </summary>
internal static class ReleaseFile
{
    internal static IReadOnlyDictionary<string, string> Parse(string releaseFilePath)
    {
        var entries = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var line in File.ReadLines(releaseFilePath))
        {
            var trimmed = line.Trim();
            if (trimmed.Length == 0 || trimmed.StartsWith('#'))
                continue;

            var separator = trimmed.IndexOf('=');
            if (separator <= 0)
                continue;

            var key = trimmed[..separator].Trim();
            entries[key] = Unquote(trimmed[(separator + 1)..].Trim());
        }

        return entries;
    }

    /// <summary>Extracts the feature version: <c>21.0.5</c> → 21, <c>1.8.0_402</c> → 8, <c>25-ea</c> → 25.</summary>
    internal static int? TryGetLanguageVersion(string? javaVersion)
    {
        if (string.IsNullOrWhiteSpace(javaVersion))
            return null;

        var head = CutAt(javaVersion, '-', '+', '_');
        var segments = head.Split('.');
        var first = LeadingDigits(segments[0]);

        // Legacy 1.8.0_xxx notation: the feature version is the second segment.
        return segments.Length > 1 && first == 1 ? LeadingDigits(segments[1]) : first;
    }

    private static string CutAt(string value, params char[] separators)
    {
        var cut = value.IndexOfAny(separators);
        return cut < 0 ? value : value[..cut];
    }

    private static int? LeadingDigits(string segment)
    {
        var count = 0;
        while (count < segment.Length && char.IsDigit(segment[count]))
            count++;

        return count == 0 ? null : int.Parse(segment[..count], CultureInfo.InvariantCulture);
    }

    private static string Unquote(string value)
    {
        // Quotes must be paired, otherwise return the value as-is (no silent character eating on malformed input).
        if (value.Length < 2 || value[0] != '"' || value[^1] != '"')
            return value;

        var builder = new StringBuilder(value.Length);
        var end = value.Length - 1;
        for (var i = 1; i < end; i++)
        {
            if (value[i] == '\\' && i + 1 < end)
            {
                builder.Append(value[++i]);
            }
            else
            {
                builder.Append(value[i]);
            }
        }

        return builder.ToString();
    }
}
