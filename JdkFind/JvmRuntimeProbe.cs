using System.ComponentModel;
using System.Diagnostics;

namespace JdkFind;

/// <summary>
///     Executes a java executable to read its runtime system properties (the
///     Gradle approach): runtime and VM name/version are always present there,
///     while the release file's key set varies by vendor. The shared pipeline
///     behind runtime enrichment and JdkFinder's probe factories.
/// </summary>
internal static class JvmRuntimeProbe
{
    internal static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(15);

    private const int TailLength = 4096;

    /// <summary>Environment variables removed from a sanitized probe child's
    /// environment: every JVM-domain variable that can distort what the binary
    /// reports — injected options, a foreign libjvm through the dynamic loader.</summary>
    internal static readonly string[] SanitizedVariables =
    [
        "_JAVA_OPTIONS",
        "JDK_JAVA_OPTIONS",
        "JAVA_TOOL_OPTIONS",
        "CLASSPATH",
        "LD_PRELOAD",
        "LD_LIBRARY_PATH",
    ];

    internal sealed record Info(
        string Vendor,
        string JavaVersion,
        string RuntimeName,
        string RuntimeVersion,
        string VmName,
        string VmVersion,
        string OsArch);

    /// <summary>The pipeline outcome for one executable. <see cref="Failure" /> is
    /// set when the child did not run to completion successfully — a spawn error,
    /// the timeout cap, or (under <c>requireExitSuccess</c>) a non-zero exit —
    /// carrying the reason plus an output tail for display.
    /// <see cref="Properties" /> holds the parsed system properties when the
    /// output was parseable and the exit rule was satisfied;
    /// <see cref="ExitCode" /> is null when the child never
    /// ran to completion.</summary>
    internal sealed record Outcome(
        int? ExitCode,
        string? Failure,
        IReadOnlyDictionary<string, string>? Properties);

    /// <summary>Probes the candidate; null when the java executable is missing, broken, or too slow.</summary>
    internal static Info? Probe(string homePath) =>
        ProbeWithFailure(homePath).Info;

    /// <summary>Probes the candidate and additionally reports why it failed. A null
    /// info with a null failure means the java executable is missing (or ran but
    /// printed nothing parseable — not a start failure).</summary>
    internal static (Info? Info, string? Failure) ProbeWithFailure(string homePath)
    {
        var java = JavaHomeLayout.JavaExecutablePath(homePath);
        if (!File.Exists(java))
            return (null, null);

        var outcome = Run(java, DefaultTimeout, requireExitSuccess: false, sanitizeEnvironment: false);
        return outcome.Properties is null ? (null, outcome.Failure) : (Project(outcome.Properties), null);
    }

    /// <summary>Runs the probe pipeline against one executable: spawn, drain both
    /// streams, wait under the timeout (killing the child tree on overrun), then
    /// parse. With <paramref name="requireExitSuccess" /> a non-zero exit counts as
    /// a failure; with <paramref name="sanitizeEnvironment" /> the JVM-domain
    /// environment variables are removed from the child.</summary>
    internal static Outcome Run(string javaExecutablePath, TimeSpan timeout, bool requireExitSuccess, bool sanitizeEnvironment)
    {
        var startInfo = CreateStartInfo(javaExecutablePath, sanitizeEnvironment);

        Process process;
        try
        {
            process = Process.Start(startInfo)!;
        }
        catch (Exception exception) when (
            exception is Win32Exception or IOException or UnauthorizedAccessException or System.Security.SecurityException)
        {
            return new Outcome(null, exception.Message, null);
        }

        using (process)
        {
            var standardError = process.StandardError.ReadToEndAsync();
            var standardOutput = process.StandardOutput.ReadToEndAsync();
            if (!process.WaitForExit(timeout))
            {
                TryKill(process);
                return new Outcome(null, TimeoutFailure(timeout), null);
            }

            var exitCode = process.ExitCode;
            var error = standardError.GetAwaiter().GetResult();
            var output = standardOutput.GetAwaiter().GetResult();
            if (requireExitSuccess && exitCode != 0)
                return new Outcome(exitCode, ExitFailure(exitCode, error, output), null);

            return new Outcome(exitCode, null, ParseProperties(error));
        }
    }

    /// <summary>
    ///     Cancellation-aware counterpart of <see cref="Probe" />. A user cancellation
    ///     kills the child process and propagates; the internal timeout still degrades
    ///     to null, exactly like the synchronous probe.
    /// </summary>
    internal static async Task<Info?> ProbeAsync(string homePath, CancellationToken cancellationToken) =>
        (await ProbeWithFailureAsync(homePath, cancellationToken).ConfigureAwait(false)).Info;

    /// <summary>Async twin of <see cref="ProbeWithFailure" />; a user cancellation
    /// kills the child and propagates.</summary>
    internal static async Task<(Info? Info, string? Failure)> ProbeWithFailureAsync(
        string homePath, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var java = JavaHomeLayout.JavaExecutablePath(homePath);
        if (!File.Exists(java))
            return (null, null);

        var outcome = await RunAsync(java, DefaultTimeout, requireExitSuccess: false, sanitizeEnvironment: false, cancellationToken).ConfigureAwait(false);
        return outcome.Properties is null ? (null, outcome.Failure) : (Project(outcome.Properties), null);
    }

    /// <summary>Async twin of <see cref="Run" />; a user cancellation kills the child
    /// process and propagates.</summary>
    internal static async Task<Outcome> RunAsync(
        string javaExecutablePath, TimeSpan timeout, bool requireExitSuccess, bool sanitizeEnvironment, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        Process process;
        try
        {
            process = Process.Start(CreateStartInfo(javaExecutablePath, sanitizeEnvironment))!;
        }
        catch (Exception exception) when (
            exception is Win32Exception or IOException or UnauthorizedAccessException or System.Security.SecurityException)
        {
            return new Outcome(null, exception.Message, null);
        }

        using (process)
        {
            try
            {
                var standardError = process.StandardError.ReadToEndAsync(cancellationToken);

                // The stdout stream carries nothing the probe parses; without the
                // token it simply completes when the pipes close.
                var standardOutput = process.StandardOutput.ReadToEndAsync();

                // The timeout feeds a linked source so a hung child degrades to a
                // timeout failure; the caller's token distinguishes a user cancel.
                using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                timeoutSource.CancelAfter(timeout);
                try
                {
                    await process.WaitForExitAsync(timeoutSource.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
                {
                    TryKill(process);
                    return new Outcome(null, TimeoutFailure(timeout), null);
                }

                var exitCode = process.ExitCode;
                var error = await standardError.ConfigureAwait(false);
                var output = await standardOutput.ConfigureAwait(false);
                if (requireExitSuccess && exitCode != 0)
                    return new Outcome(exitCode, ExitFailure(exitCode, error, output), null);

                return new Outcome(exitCode, null, ParseProperties(error));
            }
            catch (OperationCanceledException)
            {
                TryKill(process);
                throw;
            }
        }
    }

    /// <summary>Returns the environment without the JVM-domain variables that can
    /// distort what the binary reports.</summary>
    internal static IDictionary<string, string?> SanitizeEnvironment(IDictionary<string, string?> environment)
    {
        foreach (var variable in SanitizedVariables)
            environment.Remove(variable);

        return environment;
    }

    private static ProcessStartInfo CreateStartInfo(string javaExecutablePath, bool sanitizeEnvironment)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = javaExecutablePath,
            Arguments = "-XshowSettings:properties -version",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        if (sanitizeEnvironment)
            SanitizeEnvironment(startInfo.Environment);

        return startInfo;
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

    private static IReadOnlyDictionary<string, string>? ParseProperties(string stderr)
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

        return properties.ContainsKey("java.version") ? properties : null;
    }

    private static Info Project(IReadOnlyDictionary<string, string> properties) => new(
        properties.GetValueOrDefault("java.vendor") ?? string.Empty,
        properties["java.version"],
        properties.GetValueOrDefault("java.runtime.name") ?? string.Empty,
        properties.GetValueOrDefault("java.runtime.version") ?? string.Empty,
        properties.GetValueOrDefault("java.vm.name") ?? string.Empty,
        properties.GetValueOrDefault("java.vm.version") ?? string.Empty,
        properties.GetValueOrDefault("os.arch") ?? string.Empty);

    private static string ExitFailure(int exitCode, string standardError, string standardOutput)
    {
        // The child's stderr is what launchers display; fall back to stdout when the
        // failure produced nothing there. Bounded so a chatty child cannot flood.
        var tail = standardError.Length > 0 ? standardError : standardOutput;
        tail = tail.Length <= TailLength ? tail : tail[^TailLength..];

        return tail.Length > 0 ? $"exit code {exitCode}{Environment.NewLine}{tail}" : $"exit code {exitCode}";
    }

    private static string TimeoutFailure(TimeSpan timeout) =>
        $"the probe timed out after {timeout.TotalSeconds:0.#}s";
}
