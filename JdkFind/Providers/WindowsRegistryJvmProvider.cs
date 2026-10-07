using System.Runtime.Versioning;
using System.Security;
using Microsoft.Win32;

namespace JdkFind.Providers;

/// <summary>
///     Reads <c>JavaHome</c> values from the Windows registry under the vendor roots
///     known to write them (JavaSoft, Eclipse Adoptium, Microsoft, Azul Systems,
///     Amazon Corretto, AdoptOpenJDK, IBM Semeru, BellSoft). Each root is walked to
///     a small bounded depth so vendor-specific nesting (e.g.
///     <c>...\JDK\21\hotspot\MSI</c>) is covered without broad scans. Both the
///     64-bit and 32-bit registry views are read. Scope note: HKCU is not read
///     (machine-wide installs only), and vendor roots outside this list (Red Hat,
///     ...) are intentionally out of scope.
/// </summary>
public sealed class WindowsRegistryJvmProvider : IJvmProvider
{
    private static readonly (string Path, int Depth)[] DefaultRoots =
    [
        (@"SOFTWARE\JavaSoft", 2),
        (@"SOFTWARE\Eclipse Adoptium\JDK", 3),
        (@"SOFTWARE\Microsoft\JDK", 3),
        (@"SOFTWARE\Azul Systems\Zulu", 3),
        (@"SOFTWARE\Amazon Corretto", 3),
        (@"SOFTWARE\AdoptOpenJDK\JDK", 3),
        (@"SOFTWARE\Semeru\JDK", 3),
        (@"SOFTWARE\BellSoft", 3),
    ];

    private readonly (string Path, int Depth)[] roots;

    /// <summary>Scans the vendor roots known to publish <c>JavaHome</c> values.</summary>
    public WindowsRegistryJvmProvider() : this(DefaultRoots)
    {
    }

    /// <summary>Scans a single explicit registry root (relative to HKLM's 64-bit view)
    /// down to <paramref name="subKeyDepth" /> subkey levels.</summary>
    public WindowsRegistryJvmProvider(string rootPath, int subKeyDepth)
        : this([(rootPath, subKeyDepth)])
    {
    }

    private WindowsRegistryJvmProvider((string Path, int Depth)[] roots) => this.roots = roots;

    /// <inheritdoc />
    public string Name => "windows-registry";

    /// <inheritdoc />
    public IEnumerable<string> GetJavaHomes() =>
        OperatingSystem.IsWindows() ? CollectHomes() : [];

    [SupportedOSPlatform("windows")]
    private List<string> CollectHomes()
    {
        var homes = new List<string>();
        try
        {
            // Both views: the 64-bit keys plus the WOW6432Node mirrors the 32-bit
            // view exposes; identical homes deduplicate at the facade.
            foreach (var view in new[] { RegistryView.Registry64, RegistryView.Registry32 })
            {
                using var baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, view);
                foreach (var (rootPath, depth) in roots)
                {
                    using var root = baseKey.OpenSubKey(rootPath);
                    if (root is not null)
                        Collect(root, depth, homes);
                }
            }
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or SecurityException)
        {
            // An unreadable registry counts as no results.
        }

        return homes;
    }

    [SupportedOSPlatform("windows")]
    private static void Collect(RegistryKey key, int remainingDepth, ICollection<string> homes)
    {
        // BellSoft's MSI writes InstallationPath; the other vendors write JavaHome.
        if (key.GetValue("JavaHome") is string { Length: > 0 } javaHome
            && JavaHomeLayout.Probe(javaHome) is { } home)
        {
            homes.Add(home);
            return;
        }

        if (key.GetValue("InstallationPath") is string { Length: > 0 } installationPath
            && JavaHomeLayout.Probe(installationPath) is { } installHome)
        {
            homes.Add(installHome);
            return;
        }

        if (remainingDepth == 0)
            return;

        foreach (var name in key.GetSubKeyNames())
        {
            try
            {
                using var subKey = key.OpenSubKey(name);
                if (subKey is not null)
                    Collect(subKey, remainingDepth - 1, homes);
            }
            catch (Exception exception) when (
                exception is IOException or UnauthorizedAccessException or SecurityException)
            {
                // Skip inaccessible keys.
            }
        }
    }
}
