using System.Runtime.Versioning;
using Microsoft.Win32;

namespace JdkFind.Providers;

/// <summary>
///     Reads <c>JavaHome</c> values from the Windows registry under the vendor roots
///     known to write them (JavaSoft, Eclipse Adoptium, Microsoft, Azul Systems, Amazon
///     Corretto). Each root is walked to a small bounded depth so vendor-specific
///     nesting (e.g. <c>...\JDK\21\hotspot\MSI</c>) is covered without broad scans.
///     Scope note: only the 64-bit registry view is read (32-bit installs registered
///     under WOW6432Node are not covered), and vendor roots outside this list
///     (Red Hat, BellSoft, ...) are intentionally out of scope.
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
    ];

    private readonly (string Path, int Depth)[] roots;

    /// <summary>Scans the vendor roots known to publish <c>JavaHome</c> values.</summary>
    public WindowsRegistryJvmProvider() : this(DefaultRoots) { }

    /// <summary>Scans a single explicit registry root (relative to HKLM's 64-bit view)
    /// down to <paramref name="subKeyDepth" /> subkey levels.</summary>
    public WindowsRegistryJvmProvider(string rootPath, int subKeyDepth)
        : this([(rootPath, subKeyDepth)]) { }

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
            using var baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);
            foreach (var (rootPath, depth) in roots)
            {
                using var root = baseKey.OpenSubKey(rootPath);
                if (root is not null)
                    Collect(root, depth, homes);
            }
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        {
            // An unreadable registry counts as no results.
        }

        return homes;
    }

    [SupportedOSPlatform("windows")]
    private static void Collect(RegistryKey key, int remainingDepth, ICollection<string> homes)
    {
        if (key.GetValue("JavaHome") is string { Length: > 0 } javaHome
            && JavaHomeLayout.Probe(javaHome) is { } home)
        {
            homes.Add(home);
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
                exception is IOException or UnauthorizedAccessException or System.Security.SecurityException)
            {
                // Skip inaccessible keys.
            }
        }
    }
}