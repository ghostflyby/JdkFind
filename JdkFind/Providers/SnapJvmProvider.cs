namespace JdkFind.Providers;

/// <summary>
///     Scans the host-side <c>/snap</c> mount points for JVMs bundled inside
///     snaps: the JetBrains <c>jbr</c> directory and the conventional
///     <c>usr/lib/jvm</c> location inside the sandbox mount. The prefixes
///     deliberately overlap — each snap's <c>current</c> directory (whose
///     children carry the jbr) plus its <c>usr/lib/jvm</c> (whose children are
///     the sandbox JDKs). The facade's canonical-path deduplication folds these
///     onto the same physical homes the in-sandbox <c>$SNAP</c> mirror scan (the
///     unix provider) finds.
/// </summary>
public sealed class SnapJvmProvider(string? snapRoot) : ICommonPrefixesJvmProvider
{
    /// <inheritdoc />
    public string Name => "snap";

    /// <summary>Scans the default <c>/snap</c> mount root on Linux.</summary>
    public SnapJvmProvider() : this(OperatingSystem.IsLinux() ? "/snap" : null) { }

    IEnumerable<string> ICommonPrefixesJvmProvider.GetCommonPrefixes() =>
        JvmScanning.EnumerateGuarded(snapRoot ?? string.Empty)
            .SelectMany(snap => new[]
            {
                Path.Combine(snap, "current"),
                Path.Combine(snap, "current", "usr", "lib", "jvm"),
            });
}
