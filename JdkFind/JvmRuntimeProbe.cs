using System.ComponentModel;
using System.Diagnostics;

namespace JdkFind;

/// <summary>
///     Executes the candidate's own java executable to read its runtime system
///     properties (the Gradle approach): runtime and VM name/version are always
///     present there, while the release file's key set varies by vendor.
/// </summary>
internal static class JvmRuntimeProbe
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(15);

    internal sealed record Info(
        string Vendor,
        string JavaVersion,
        string RuntimeName,
        string RuntimeVersion,
        string VmName,
        string VmVersion,
        string OsArch);

    /// <summary>Probes the candidate; null when the java executable is missing, broken, or too slow.</summary>
    internal static Info? Probe(string homePath)
    {
        var java = Path.Combine(homePath, "bin", JavaHomeLayout.JavaExecutableName);
        if (!File.Exists(java))
            return null;

        var startInfo = new ProcessStartInfo
        {
            FileName = java,
            Arguments = "-XshowSettings:properties -version",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        Process process;
        try
        {
            process = Process.Start(startInfo)!;
        }
        catch (Exception exception) when (
            exception is Win32Exception or IOException or UnauthorizedAccessException or System.Security.SecurityException)
        {
            return null;
        }

        using (process)
        {
            var stderr = process.StandardError.ReadToEndAsync();
            _ = process.StandardOutput.ReadToEndAsync();
            if (!process.WaitForExit(Timeout))
            {
                process.Kill(true);
                return null;
            }

            return Parse(stderr.GetAwaiter().GetResult());
        }
    }

    private static Info? Parse(string stderr)
    {
        var properties = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var line in stderr.Split('\n'))
        {
            // Only single-token keys belong to the properties section; skip anything else.
            var separator = line.IndexOf(" = ", StringComparison.Ordinal);
            if (separator < 0)
                continue;

            var key = line[..separator].Trim();
            if (key.Contains(' '))
                continue;

            properties[key] = line[(separator + 3)..].Trim();
        }

        if (!properties.TryGetValue("java.version", out var javaVersion))
            return null;

        return new Info(
            properties.GetValueOrDefault("java.vendor") ?? string.Empty,
            javaVersion,
            properties.GetValueOrDefault("java.runtime.name") ?? string.Empty,
            properties.GetValueOrDefault("java.runtime.version") ?? string.Empty,
            properties.GetValueOrDefault("java.vm.name") ?? string.Empty,
            properties.GetValueOrDefault("java.vm.version") ?? string.Empty,
            properties.GetValueOrDefault("os.arch") ?? string.Empty);
    }
}
