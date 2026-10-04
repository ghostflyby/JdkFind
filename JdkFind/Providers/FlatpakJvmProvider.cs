namespace JdkFind.Providers;

/// <summary>
///     Scans the JVMs visible inside a flatpak sandbox: the shared runtime
///     extensions mounted at <c>/usr/lib/sdk/&lt;extension&gt;/jvm/&lt;version&gt;</c>
///     (JDKs installed via <c>flatpak install
///     org.freedesktop.Sdk.Extension.openjdk//…</c> and shared by every app on the
///     same runtime) and the app-bundled <c>/app/jdk</c>. Active only inside a
///     flatpak sandbox (detected via <c>/.flatpak-info</c> or
///     <c>$FLATPAK_ID</c>) — that check is the positive proof that <c>/app</c> is
///     this flatpak instance's app directory, so attributing it to flatpak is
///     exact.
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
                yield return candidate;

        // /app is the flatpak app directory; its bundled jdk is part of the
        // flatpak result for this run.
        foreach (var home in JvmScanning.EnumerateGuarded(AppDirectory(sdkRoot)))
            yield return home;
    }

    private static string AppDirectory(string sdkRoot) =>
        Path.GetFullPath(Path.Combine(sdkRoot, "..", "..", "..", "app"));

    private static bool InFlatpakSandbox() =>
        Environment.GetEnvironmentVariable("FLATPAK_ID") is not null || File.Exists("/.flatpak-info");
}
