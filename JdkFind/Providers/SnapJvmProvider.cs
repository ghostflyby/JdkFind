namespace JdkFind.Providers;

/// <summary>
///     Scans the host-side <c>/snap</c> mount points for JVMs bundled inside
///     snaps: the conventional <c>usr/lib/jvm</c> location inside the sandbox
///     mount and the <c>jbr</c> directory the JetBrains IDE snaps carry. The
///     facade's canonical-path deduplication folds these onto the same physical
///     homes the in-sandbox <c>$SNAP</c> mirror scan (the unix provider) finds.
/// </summary>
public sealed class SnapJvmProvider(string? snapRoot) : IJvmProvider
{
    /// <inheritdoc />
    public string Name => "snap";

    /// <summary>Scans the default <c>/snap</c> mount root on Linux.</summary>
    public SnapJvmProvider() : this(OperatingSystem.IsLinux() ? "/snap" : null) { }

    IEnumerable<string> IJvmProvider.GetJavaHomes()
    {
        foreach (var snap in JvmScanning.EnumerateGuarded(snapRoot ?? string.Empty))
        {
            foreach (var candidate in JvmScanning.EnumerateGuarded(Path.Combine(snap, "current", "usr", "lib", "jvm")))
                if (JavaHomeLayout.Probe(candidate) is { } home)
                    yield return home;

            if (JavaHomeLayout.Probe(Path.Combine(snap, "current", "jbr")) is { } jbr)
                yield return jbr;
        }
    }
}
