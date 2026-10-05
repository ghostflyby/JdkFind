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
    ///     Execute each candidate's own java executable to enrich the metadata with
    ///     runtime properties (runtime/VM name and version, vendor fallback). Adds a
    ///     few hundred milliseconds per installation; failures degrade silently to
    ///     the release-file metadata. Default is true.
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

        // The runtime probe executes the installation's own java executable; anything
        // it adds is enrichment — release-file values keep precedence.
        var runtime = probeRuntime ? JvmRuntimeProbe.Probe(homePath) : null;

        return Build(homePath, providers, release, runtime);
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

        // The runtime probe executes the installation's own java executable; anything
        // it adds is enrichment — release-file values keep precedence.
        var runtime = probeRuntime
            ? await JvmRuntimeProbe.ProbeAsync(entry.HomePath, cancellationToken).ConfigureAwait(false)
            : null;

        return Build(entry.HomePath, entry.Providers, release, runtime);
    }

    private static Jvm Build(
        string homePath,
        IReadOnlyList<string> providers,
        IReadOnlyDictionary<string, string> release,
        JvmRuntimeProbe.Info? runtime)
    {
        // The raw JAVA_VERSION string is preserved on JvmVersion.Original; unparseable
        // values degrade to the Unknown placeholder (sorts before every known version).
        var version = JvmVersion.Parse(release.GetValueOrDefault("JAVA_VERSION"));

        var vendorRaw = NonEmpty(release.GetValueOrDefault("IMPLEMENTOR")) ?? runtime?.Vendor;
        var implementorVersion = NonEmpty(release.GetValueOrDefault("IMPLEMENTOR_VERSION"));
        var graalVmRelease = release.ContainsKey("GRAALVM_VERSION");
        var vendor = JvmIdentity.DetectVendor(vendorRaw);
        var distribution = JvmIdentity.DetectDistribution(implementorVersion, vendorRaw, graalVmRelease);

        return new Jvm
        {
            Home = new DirectoryInfo(homePath),
            Providers = providers,
            Version = version,
            LanguageVersion = ReleaseFile.TryGetLanguageVersion(version.Original),
            HasCompiler = File.Exists(Path.Combine(homePath, "bin", JavaHomeLayout.CompilerExecutableName)),
            VendorRaw = vendorRaw,
            Vendor = vendor,
            Distribution = distribution,
            RuntimeName = runtime?.RuntimeName,
            RuntimeVersion = runtime?.RuntimeVersion,
            VmName = runtime?.VmName,
            VmVersion = runtime?.VmVersion,
            OsName = release.GetValueOrDefault("OS_NAME"),
            Architecture = NonEmpty(release.GetValueOrDefault("OS_ARCH")) ?? runtime?.OsArch,
        };
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