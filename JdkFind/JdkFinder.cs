using JdkFind.Providers;

namespace JdkFind;

/// <summary>
///     A configured JVM finder. Instances are immutable and stateless between
///     calls — every <see cref="Locate" /> is a fresh scan of the machine and
///     nothing is cached. Customize through a with expression.
/// </summary>
public sealed record JdkFinder
{
    private static readonly StringComparer PathComparer =
        OperatingSystem.IsWindows() || OperatingSystem.IsMacOS()
            ? StringComparer.OrdinalIgnoreCase
            : StringComparer.Ordinal;

    /// <summary>Sources consulted in order; candidates reported by several sources
    /// merge into one <see cref="Jvm" /> listing every source. Defaults to no
    /// sources at all — start from <see cref="Default" /> or set this explicitly.</summary>
    public IReadOnlyList<IJvmProvider> Providers { get; init; } = [];

    /// <summary>Collapse candidates that resolve to the same physical directory. Default is true.</summary>
    public bool DeduplicateHomes { get; init; } = true;

    /// <summary>
    ///     Execute each candidate's own java executable, probing its runtime
    ///     properties (runtime/VM name and version, vendor, architecture, OS
    ///     name) onto the installation's <see cref="Jvm.Executable" />. Adds a
    ///     few hundred milliseconds per installation; failures degrade silently —
    ///     the release-file metadata stands and <see cref="Jvm.Executable" />
    ///     carries only the path. Default is true.
    /// </summary>
    public bool ProbeRuntimeProperties { get; init; } = true;

    /// <summary>The shared ready-to-use finder bound to the platform's built-in sources.</summary>
    public static JdkFinder Default { get; } = new() { Providers = CreateDefaultProviders() };

    /// <summary>
    ///     Locates JVMs from all sources, in source order. Every source reports
    ///     before results are produced, so each <see cref="Jvm" /> can list all the
    ///     sources that found it.
    /// </summary>
    public IEnumerable<Jvm> Locate() => Enumerate(this);

    /// <summary>
    ///     Async counterpart of <see cref="Locate" /> for callers that own a
    ///     cancellation token. The discovery stays local I/O; the token buys a prompt
    ///     abort of the runtime probes (the child java processes are killed), and
    ///     release-file reads check the token before starting. Collect-then-merge
    ///     like the sync pipeline.
    /// </summary>
    public async Task<IReadOnlyList<Jvm>> LocateAsync(CancellationToken cancellationToken = default)
    {
        var order = CollectCandidates(this, cancellationToken);

        var results = new Jvm?[order.Count];
        if (ProbeRuntimeProperties)
        {
            // One process per installation is the slow part: probe concurrently,
            // bounded like PLINQ's default, keeping the first-discovery order.
            await Parallel.ForEachAsync(
                Enumerable.Range(0, order.Count),
                new ParallelOptions
                {
                    MaxDegreeOfParallelism = Environment.ProcessorCount,
                    CancellationToken = cancellationToken,
                },
                async (index, token) =>
                    results[index] = await CreateJvmAsync(order[index], probeRuntime: true, token).ConfigureAwait(false))
                .ConfigureAwait(false);
        }
        else
        {
            for (var index = 0; index < order.Count; index++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                results[index] = await CreateJvmAsync(order[index], probeRuntime: false, cancellationToken).ConfigureAwait(false);
            }
        }

        return results.OfType<Jvm>().ToArray();
    }

    /// <summary>
    ///     Probes the java executable at <paramref name="javaExecutablePath" /> —
    ///     taken verbatim, with no name normalization and no layout requirement —
    ///     and returns the <see cref="JavaExecutable" /> it yields, or null when no
    ///     JVM can be established. Where a home is derived by walking up from the
    ///     binary (<c>bin</c>, <c>jre/bin</c> and macOS bundle layouts), its
    ///     release file fills what the binary itself does not report — the values
    ///     the binary reports take precedence. The probe runs the child with
    ///     JVM-affecting environment variables removed, under the built-in
    ///     15-second cap (overridable via <paramref name="timeout" />); a child
    ///     that does not run to completion surfaces as
    ///     <see cref="JavaExecutable.StartFailure" />.
    /// </summary>
    public JavaExecutable? FromExecutable(string javaExecutablePath, TimeSpan? timeout = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(javaExecutablePath);

        var fullPath = Path.GetFullPath(javaExecutablePath);
        string? home = null;
        if (Path.GetDirectoryName(fullPath) is { } directory)
        {
            home = JavaHomeLayout.Probe(directory)
                ?? (Path.GetDirectoryName(directory) is { } parentDirectory ? JavaHomeLayout.Probe(parentDirectory) : null);
        }

        var outcome = JvmRuntimeProbe.Run(
            javaExecutablePath, timeout ?? JvmRuntimeProbe.DefaultTimeout, requireExitSuccess: true, sanitizeEnvironment: true);
        var release = TryParseRelease(home);
        if (outcome.Properties?.ContainsKey("java.version") is not true && release?.ContainsKey("JAVA_VERSION") is not true)
            return null;

        return BuildExecutable(fullPath, release, outcome);
    }

    /// <summary>Async twin of <see cref="FromExecutable" />; a cancellation token
    /// kills the child process and propagates. The child is still killed at the
    /// built-in 15-second cap — bound the wait yourself by passing a token
    /// cancelled earlier (e.g. via <c>CancellationTokenSource.CancelAfter</c>).</summary>
    public async Task<JavaExecutable?> FromExecutableAsync(string javaExecutablePath, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(javaExecutablePath);

        var fullPath = Path.GetFullPath(javaExecutablePath);
        string? home = null;
        if (Path.GetDirectoryName(fullPath) is { } directory)
        {
            home = JavaHomeLayout.Probe(directory)
                ?? (Path.GetDirectoryName(directory) is { } parentDirectory ? JavaHomeLayout.Probe(parentDirectory) : null);
        }

        var outcome = await JvmRuntimeProbe.RunAsync(
                javaExecutablePath, JvmRuntimeProbe.DefaultTimeout, requireExitSuccess: true, sanitizeEnvironment: true, cancellationToken)
            .ConfigureAwait(false);
        var release = TryParseRelease(home);
        if (outcome.Properties?.ContainsKey("java.version") is not true && release?.ContainsKey("JAVA_VERSION") is not true)
            return null;

        return BuildExecutable(fullPath, release, outcome);
    }

    /// <summary>
    ///     Probes a java home directory under any layout
    ///     <see cref="JavaHomeLayout.Probe" /> recognizes — relative paths resolve
    ///     against the process working directory and the returned
    ///     <see cref="Jvm.Home" /> is absolute — running the home's own java
    ///     executable. The returned home is the probed layout root — a JDK 8
    ///     inner-JRE directory stays the inner JRE — and the probe semantics
    ///     (environment, timeout override, precedence) match
    ///     <see cref="FromExecutable" />, with the verdict and enrichment landing
    ///     on <see cref="Jvm.Executable" />. Null when the directory is not a java
    ///     home, or when its release file turns out to be unreadable.
    /// </summary>
    public Jvm? FromHome(string homeDirectory, TimeSpan? timeout = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(homeDirectory);

        var home = JavaHomeLayout.Probe(Path.GetFullPath(homeDirectory));
        if (home is null)
            return null;

        var outcome = JvmRuntimeProbe.Run(
            JavaHomeLayout.JavaExecutablePath(home), timeout ?? JvmRuntimeProbe.DefaultTimeout,
            requireExitSuccess: true, sanitizeEnvironment: true);
        return BuildHomeJvm(home, outcome, TryParseRelease(home));
    }

    /// <summary>Async twin of <see cref="FromHome" />; a cancellation token kills
    /// the child process and propagates. The child is still killed at the built-in
    /// 15-second cap — bound the wait yourself by passing a token cancelled
    /// earlier (e.g. via <c>CancellationTokenSource.CancelAfter</c>).</summary>
    public async Task<Jvm?> FromHomeAsync(string homeDirectory, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(homeDirectory);

        var home = JavaHomeLayout.Probe(Path.GetFullPath(homeDirectory));
        if (home is null)
            return null;

        var outcome = await JvmRuntimeProbe.RunAsync(
                JavaHomeLayout.JavaExecutablePath(home), JvmRuntimeProbe.DefaultTimeout,
                requireExitSuccess: true, sanitizeEnvironment: true, cancellationToken)
            .ConfigureAwait(false);
        return BuildHomeJvm(home, outcome, TryParseRelease(home));
    }

    /// <summary>The built-in provider set for the current platform, in priority order.</summary>
    private static List<IJvmProvider> CreateDefaultProviders() =>
    [
        new JavaHomeJvmProvider(),
        new PathJvmProvider(),
        new MacOsJvmProvider(),
        new UnixJvmProvider(),
        new FlatpakJvmProvider(),
        new HomebrewJvmProvider(),
        new WindowsProgramFilesJvmProvider(),
        new WindowsRegistryJvmProvider(),
        new IntelliJJvmProvider(),
        new SdkmanJvmProvider(),
        new AsdfJvmProvider(),
        new GradleJvmProvider(),
        new JabbaJvmProvider(),
        new ScoopJvmProvider(),
    ];

    private static IEnumerable<Jvm> Enumerate(JdkFinder finder)
    {
        // Jvm.Providers lists every source that reported the same directory, so results
        // can only be produced after all providers have reported — the enumeration is
        // a full collect-then-merge pass.
        var order = CollectCandidates(finder, CancellationToken.None);

        // Runtime probing is the slow part (one process per installation): probe
        // candidates concurrently while keeping the first-discovery order. Without
        // probing, stay on the plain sequential pipeline.
        if (finder.ProbeRuntimeProperties)
        {
            return order
                .AsParallel()
                .AsOrdered()
                .Select(entry => CreateJvm(entry.HomePath, entry.Providers, probeRuntime: true))
                .OfType<Jvm>();
        }

        return order
            .Select(entry => CreateJvm(entry.HomePath, entry.Providers, probeRuntime: false))
            .OfType<Jvm>();
    }

    private static List<(string HomePath, List<string> Providers)> CollectCandidates(
        JdkFinder finder, CancellationToken cancellationToken)
    {
        var order = new List<(string HomePath, List<string> Providers)>();
        var indexByKey = finder.DeduplicateHomes ? new Dictionary<string, int>(PathComparer) : null;

        foreach (var provider in finder.Providers)
        {
            cancellationToken.ThrowIfCancellationRequested();
            foreach (var candidate in provider.GetJavaHomes())
            {
                if (indexByKey is null)
                {
                    order.Add((candidate, [provider.Name]));
                    continue;
                }

                var key = GetDeduplicationKey(candidate);
                if (indexByKey.TryGetValue(key, out var index))
                {
                    // A repeated candidate from the same source records the name once
                    // (e.g. two PATH entries leading to the same directory).
                    if (!order[index].Providers.Contains(provider.Name))
                        order[index].Providers.Add(provider.Name);
                }
                else
                {
                    indexByKey[key] = order.Count;
                    order.Add((candidate, [provider.Name]));
                }
            }
        }

        return order;
    }

    private static Jvm? CreateJvm(string homePath, IReadOnlyList<string> providers, bool probeRuntime)
    {
        // The provider contract guarantees validated homes, so the release file is
        // expected to exist; parse failures (missing or unreadable) count as no JVM.
        IReadOnlyDictionary<string, string> release;
        try
        {
            release = ReleaseFile.Parse(Path.Combine(homePath, "release"));
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        {
            // An unreadable release file (access denial, ...) counts as no JVM; one bad directory must not kill the scan.
            return null;
        }

        // The runtime probe executes the installation's own java executable; its
        // verdict and enrichment land on Executable.
        var outcome = probeRuntime ? JvmRuntimeProbe.ProbeWithFailure(homePath) : null;

        return Build(homePath, providers, release, outcome);
    }

    /// <summary>Async twin of <see cref="CreateJvm" /> for cancellation-aware callers;
    /// a cancelled token propagates instead of counting as a broken installation.</summary>
    private static async Task<Jvm?> CreateJvmAsync(
        (string HomePath, List<string> Providers) entry, bool probeRuntime, CancellationToken cancellationToken)
    {
        // The provider contract guarantees validated homes, so the release file is
        // expected to exist; parse failures (missing or unreadable) count as no JVM.
        IReadOnlyDictionary<string, string> release;
        try
        {
            release = ReleaseFile.Parse(Path.Combine(entry.HomePath, "release"), cancellationToken);
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        {
            // An unreadable release file (access denial, ...) counts as no JVM; one bad directory must not kill the scan.
            return null;
        }

        // The runtime probe executes the installation's own java executable; its
        // verdict and enrichment land on Executable.
        var outcome = probeRuntime
            ? await JvmRuntimeProbe.ProbeWithFailureAsync(entry.HomePath, cancellationToken).ConfigureAwait(false)
            : null;

        return Build(entry.HomePath, entry.Providers, release, outcome);
    }

    private static Jvm Build(
        string homePath,
        IReadOnlyList<string> providers,
        IReadOnlyDictionary<string, string> release,
        JvmRuntimeProbe.Outcome? outcome)
    {
        // The raw JAVA_VERSION string is preserved on JvmVersion.Original; unparseable
        // values degrade to the Unknown placeholder (sorts before every known version).
        var version = JvmVersion.Parse(release.GetValueOrDefault("JAVA_VERSION"));
        var vendorRaw = NonEmpty(release.GetValueOrDefault("IMPLEMENTOR"));

        return new Jvm
        {
            Home = new DirectoryInfo(homePath),
            Providers = providers,
            Executable = BuildExecutable(JavaHomeLayout.JavaExecutablePath(homePath), release, outcome),
            Version = version,
            LanguageVersion = ReleaseFile.TryGetLanguageVersion(version.Original),
            HasCompiler = File.Exists(Path.Combine(homePath, "bin", JavaHomeLayout.CompilerExecutableName)),
            VendorRaw = vendorRaw,
            Vendor = JvmIdentity.DetectVendor(vendorRaw),
            Distribution = JvmIdentity.DetectDistribution(
                NonEmpty(release.GetValueOrDefault("IMPLEMENTOR_VERSION")), vendorRaw,
                release.ContainsKey("GRAALVM_VERSION")),
        };
    }

    /// <summary>Builds the installation a probed home yields. Null when the release
    /// file — expected to exist on a probed home — is unreadable.</summary>
    private static Jvm? BuildHomeJvm(
        string homePath, JvmRuntimeProbe.Outcome outcome, IReadOnlyDictionary<string, string>? release)
    {
        if (release is null)
            return null;

        var version = JvmVersion.Parse(release.GetValueOrDefault("JAVA_VERSION"));
        var vendorRaw = NonEmpty(release.GetValueOrDefault("IMPLEMENTOR"));

        return new Jvm
        {
            Home = new DirectoryInfo(homePath),
            Providers = [],
            Executable = BuildExecutable(JavaHomeLayout.JavaExecutablePath(homePath), release, outcome),
            Version = version,
            LanguageVersion = ReleaseFile.TryGetLanguageVersion(version.Original),
            HasCompiler = File.Exists(Path.Combine(homePath, "bin", JavaHomeLayout.CompilerExecutableName)),
            VendorRaw = vendorRaw,
            Vendor = JvmIdentity.DetectVendor(vendorRaw),
            Distribution = JvmIdentity.DetectDistribution(
                NonEmpty(release.GetValueOrDefault("IMPLEMENTOR_VERSION")), vendorRaw,
                release.ContainsKey("GRAALVM_VERSION")),
        };
    }

    /// <summary>Builds a java executable dossier: every property probes first and
    /// the release file fills what the binary did not report. The runtime-only
    /// properties stay null when no probe ran or the binary did not report
    /// them.</summary>
    private static JavaExecutable BuildExecutable(
        string javaExecutablePath, IReadOnlyDictionary<string, string>? release, JvmRuntimeProbe.Outcome? outcome)
    {
        var properties = outcome?.Properties;
        var vendorRaw = NonEmpty(properties?.GetValueOrDefault("java.vendor"))
            ?? NonEmpty(release?.GetValueOrDefault("IMPLEMENTOR"));

        return new JavaExecutable
        {
            Path = javaExecutablePath,
            StartFailure = outcome?.Failure,
            Version = JvmVersion.Parse(
                properties?.GetValueOrDefault("java.version") ?? release?.GetValueOrDefault("JAVA_VERSION")),
            VendorRaw = vendorRaw,
            Vendor = JvmIdentity.DetectVendor(vendorRaw),
            OsName = NonEmpty(properties?.GetValueOrDefault("os.name")) ?? release?.GetValueOrDefault("OS_NAME"),
            Architecture = NonEmpty(properties?.GetValueOrDefault("os.arch"))
                ?? NonEmpty(release?.GetValueOrDefault("OS_ARCH")),
            RuntimeName = properties?.GetValueOrDefault("java.runtime.name"),
            RuntimeVersion = properties?.GetValueOrDefault("java.runtime.version"),
            VmName = properties?.GetValueOrDefault("java.vm.name"),
            VmVersion = properties?.GetValueOrDefault("java.vm.version"),
        };
    }

    /// <summary>Parses the home's release file for the probe factories, where a
    /// missing or unreadable file simply means less metadata — the probe result
    /// still stands.</summary>
    private static IReadOnlyDictionary<string, string>? TryParseRelease(string? homePath)
    {
        if (homePath is null)
            return null;

        try
        {
            return ReleaseFile.Parse(Path.Combine(homePath, "release"));
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        {
            return null;
        }
    }

    private static string? NonEmpty(string? value) =>
        string.IsNullOrEmpty(value) ? null : value;

    /// <summary>
    ///     Deduplication key: the canonical path of the home (every path component's
    ///     symbolic link expanded, collapsing Homebrew's opt → Cellar and macOS
    ///     <c>/Library/Java</c> aliases); compared case-insensitively on the
    ///     case-insensitive platforms.
    /// </summary>
    private static string GetDeduplicationKey(string path)
    {
        try
        {
            return ResolveSymlinks(Path.TrimEndingDirectorySeparator(Path.GetFullPath(path)));
        }
        catch (Exception exception) when (exception is IOException or System.Security.SecurityException
                                              or ArgumentException)
        {
            return path;
        }
    }

    /// <summary>Expands every symbolic link along the path, bounding the chain to avoid link loops.</summary>
    internal static string ResolveSymlinks(string fullPath, int remainingHops = 32)
    {
        if (remainingHops == 0)
            return fullPath;

        var root = Path.GetPathRoot(fullPath) ?? string.Empty;
        var segments = fullPath[root.Length..].Split('/', '\\', StringSplitOptions.RemoveEmptyEntries);
        var resolved = root;
        for (var index = 0; index < segments.Length; index++)
        {
            resolved = Path.Combine(resolved, segments[index]);
            string? target;
            try
            {
                target = new DirectoryInfo(resolved).LinkTarget;
            }
            catch (Exception exception) when (exception is IOException or System.Security.SecurityException)
            {
                return fullPath;
            }

            if (target is null)
                continue;

            // The link target may be relative, and the joined remainder may still contain links; re-resolve.
            var targetFull = Path.GetFullPath(target, Path.GetDirectoryName(resolved)!);
            var rest = string.Join(Path.DirectorySeparatorChar, segments[(index + 1)..]);
            var combined = rest.Length == 0 ? targetFull : Path.Combine(targetFull, rest);
            return ResolveSymlinks(combined, remainingHops - 1);
        }

        return fullPath;
    }
}