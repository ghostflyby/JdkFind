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
                TryKill(process);
                return null;
            }

            return Parse(stderr.GetAwaiter().GetResult());
        }
    }

    /// <summary>
    ///     Cancellation-aware counterpart of <see cref="Probe" />. A user cancellation
    ///     kills the child process and propagates; the internal timeout still degrades
    ///     to null, exactly like the synchronous probe.
    /// </summary>
    internal static async Task<Info?> ProbeAsync(string homePath, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

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
            try
            {
                var stderr = process.StandardError.ReadToEndAsync(cancellationToken);

                // The stdout stream carries nothing the probe uses; without the token
                // it simply completes when the kill below closes the pipe, and the
                // discarded task can never end cancelled.
                _ = process.StandardOutput.ReadToEndAsync();

                // The 15-second cap feeds a linked source so a hung child degrades to
                // null like before; the caller's token distinguishes a user cancel.
                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                timeout.CancelAfter(Timeout);
                try
                {
                    await process.WaitForExitAsync(timeout.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
                {
                    TryKill(process);
                    return null;
                }

                return Parse(await stderr.ConfigureAwait(false));
            }
            catch (OperationCanceledException)
            {
                TryKill(process);
                throw;
            }
        }
    }

    /// <summary>Kills the child best-effort: it may exit between the wait and the kill,
    /// and a failed kill must never abort the whole scan.</summary>
    private static void TryKill(Process process)
    {
        try
        {
            if (!process.HasExited)
                process.Kill(true);
        }
        catch (Exception exception) when (exception is InvalidOperationException or Win32Exception)
        {
            // Nothing left to kill.
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
