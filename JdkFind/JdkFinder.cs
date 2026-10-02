namespace JdkFind;

/// <summary>Facade for locating JVM installations across all configured providers.</summary>
public static class JdkFinder
{
    private static readonly StringComparer PathComparer =
        OperatingSystem.IsWindows() || OperatingSystem.IsMacOS()
            ? StringComparer.OrdinalIgnoreCase
            : StringComparer.Ordinal;

    /// <summary>
    ///     Locates JVMs from all configured providers, in provider order. Every provider
    ///     reports before results are produced, so each <see cref="Jvm" /> can list all
    ///     the sources that found it.
    /// </summary>
    public static IEnumerable<Jvm> Locate(JdkFindOptions? options = null)
    {
        options ??= new JdkFindOptions();
        return Enumerate(options);
    }

    /// <summary>Enumerates only the JVMs whose feature version matches.</summary>
    public static IEnumerable<Jvm> Find(int languageVersion, JdkFindOptions? options = null) =>
        Locate(options).Where(jvm => jvm.LanguageVersion == languageVersion);

    /// <summary>Returns the newest JVM found, or null when none is found.</summary>
    public static Jvm? GetNewest(JdkFindOptions? options = null)
    {
        Jvm? newest = null;
        foreach (var jvm in Locate(options))
            if (newest is null || jvm.Version > newest.Version)
                newest = jvm;

        return newest;
    }

    /// <summary>
    ///     Returns the JVM pointed to by <c>JAVA_HOME</c> when it is among the results,
    ///     otherwise the newest JVM found. Returns null when none is found.
    /// </summary>
    public static Jvm? GetDefault(JdkFindOptions? options = null)
    {
        // Single pass: when JAVA_HOME misses, take the newest from the same results instead of a second full enumeration.
        var jvms = Locate(options).ToList();

        var javaHome = Environment.GetEnvironmentVariable("JAVA_HOME");
        if (!string.IsNullOrWhiteSpace(javaHome))
        {
            var javaHomeKey = GetDeduplicationKey(javaHome);
            foreach (var jvm in jvms)
                if (PathComparer.Equals(GetDeduplicationKey(jvm.Home.FullName), javaHomeKey))
                    return jvm;
        }

        Jvm? newest = null;
        foreach (var jvm in jvms)
            if (newest is null || jvm.Version > newest.Version)
                newest = jvm;

        return newest;
    }

    private static IEnumerable<Jvm> Enumerate(JdkFindOptions options)
    {
        // Jvm.Providers lists every source that reported the same directory, so results
        // can only be produced after all providers have reported — the enumeration is
        // a full collect-then-merge pass.
        var order = new List<(string HomePath, List<string> Providers)>();
        var indexByKey = options.DeduplicateHomes ? new Dictionary<string, int>(PathComparer) : null;

        foreach (var provider in options.Providers)
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

        // Runtime probing is the slow part (one process per installation): probe
        // candidates concurrently while keeping the first-discovery order. Without
        // probing, stay on the plain sequential pipeline.
        if (options.ProbeRuntimeProperties)
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

        // The raw JAVA_VERSION string is preserved on JvmVersion.Original; unparseable
        // values degrade to the Unknown placeholder (sorts before every known version).
        var version = JvmVersion.Parse(release.GetValueOrDefault("JAVA_VERSION"));

        var vendor = NonEmpty(release.GetValueOrDefault("IMPLEMENTOR")) ?? runtime?.Vendor;
        var knownVendor = JvmVendors.Parse(vendor);

        return new Jvm
        {
            Home = new DirectoryInfo(homePath),
            Providers = providers,
            Version = version,
            LanguageVersion = ReleaseFile.TryGetLanguageVersion(version.Original),
            Vendor = vendor,
            KnownVendor = knownVendor,
            VendorDisplayName = JvmVendors.GetDisplayName(knownVendor, vendor),
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