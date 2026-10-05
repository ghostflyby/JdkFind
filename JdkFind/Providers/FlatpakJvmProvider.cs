namespace JdkFind.Providers;

/// <summary>
///     Scans the shared JVM runtime extensions flatpak mounts inside a sandbox
///     (<c>/usr/lib/sdk/&lt;extension&gt;/jvm/&lt;version&gt;</c>) — JDKs installed via
///     <c>flatpak install org.freedesktop.Sdk.Extension.openjdk//…</c> and shared by
///     every app depending on the same runtime. Active only inside a flatpak
///     sandbox (detected via <c>/.flatpak-info</c> or <c>$FLATPAK_ID</c>). The
///     app-bundled <c>/app/jdk</c> is deliberately not attributed to flatpak — it
///     belongs to the application that packaged it.
/// </summary>
public sealed class FlatpakJvmProvider : IJvmProvider
{
    private readonly string sdkRoot;
    private readonly bool active;

    /// <summary>Active only inside a flatpak sandbox.</summary>
    public FlatpakJvmProvider() : this("/usr/lib/sdk", InFlatpakSandbox()) { }

    /// <summary>Test seam: scans an explicit sdk root, bypassing the sandbox check.</summary>
    internal FlatpakJvmProvider(string sdkRoot, bool active)
    {
        this.sdkRoot = sdkRoot;
        this.active = active;
    }

    /// <inheritdoc />
    public string Name => "flatpak";

    /// <inheritdoc />
    public IEnumerable<string> GetJavaHomes()
    {
        if (!active)
            yield break;

        foreach (var extension in JvmScanning.EnumerateGuarded(sdkRoot))
            foreach (var candidate in JvmScanning.EnumerateGuarded(Path.Combine(extension, "jvm")))
                if (JavaHomeLayout.Probe(candidate) is { } home)
                    yield return home;
    }

    private static bool InFlatpakSandbox() =>
        Environment.GetEnvironmentVariable("FLATPAK_ID") is not null || File.Exists("/.flatpak-info");
}
