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
public sealed class WindowsRegistryJvmProvider : JvmProviderBase
{
    private static readonly (string Path, int Depth)[] RegistryRoots =
    [
        (@"SOFTWARE\JavaSoft", 2),
        (@"SOFTWARE\Eclipse Adoptium\JDK", 3),
        (@"SOFTWARE\Microsoft\JDK", 3),
        (@"SOFTWARE\Azul Systems\Zulu", 3),
        (@"SOFTWARE\Amazon Corretto", 3),
    ];

    public override string Name => "windows-registry";

    public override IAsyncEnumerable<string> GetJavaHomesAsync(CancellationToken cancellationToken = default) =>
        OperatingSystem.IsWindows() ? CollectHomes().ToAsyncEnumerable() : AsyncEnumerable.Empty<string>();

    [SupportedOSPlatform("windows")]
    private static IEnumerable<string> CollectHomes()
    {
        var homes = new List<string>();
        try
        {
            using var baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);
            foreach (var (rootPath, depth) in RegistryRoots)
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
        if (key.GetValue("JavaHome") is string { Length: > 0 } javaHome)
        {
            homes.Add(javaHome);
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
