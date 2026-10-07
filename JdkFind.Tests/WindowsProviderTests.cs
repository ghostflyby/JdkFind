using System.Security.Principal;
using JdkFind.Providers;
using Microsoft.Win32;

namespace JdkFind.Tests;

/// <summary>
///     Windows-source provider tests. They write real fixtures — a fake vendor
///     layout and HKLM registry keys — so they run only on Windows runners (which
///     execute elevated) and skip elsewhere.
/// </summary>
public class WindowsProviderTests : IDisposable
{
    private readonly TempDirectory temp = new();

    [Fact]
    public void ProgramFiles_ScansVendorSubdirectories()
    {
        if (!OperatingSystem.IsWindows())
            return;

        var jdk = TestJdk.Create(temp.FullPath, "21.0.5", "jdk-21");
        // GetJavaHomes is a default interface member, reachable only through the SPI.
        IJvmProvider source = new WindowsProgramFilesJvmProvider([temp.FullPath]);

        var home = Assert.Single(source.GetJavaHomes().ToList());

        Assert.Equal(jdk, home);
    }

    [Fact]
    public void Registry_ProbesJavaHomeValues_AndSkipsStaleEntries()
    {
        if (!OperatingSystem.IsWindows())
            return;

        if (!new WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator))
            return; // Writing HKLM keys requires elevation; GitHub Windows runners are elevated.

        var jdk = TestJdk.Create(temp.FullPath, "21.0.5", "registry-jdk");
        var stale = Path.Combine(temp.FullPath, "uninstalled");
        var rootPath = $@"SOFTWARE\JdkFindTests\{Guid.NewGuid():N}";

        try
        {
            using var baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);
            using var root = baseKey.CreateSubKey(rootPath);
            using (var valid = root.CreateSubKey("21"))
                valid.SetValue("JavaHome", jdk);
            using (var removed = root.CreateSubKey("17"))
                removed.SetValue("JavaHome", stale);

            var homes = new WindowsRegistryJvmProvider(rootPath, subKeyDepth: 1).GetJavaHomes().ToList();

            // The stale registration (pointing at an uninstalled directory) must be
            // probed away instead of flowing into dedup and the runtime probe.
            Assert.Equal([jdk], homes);
        }
        finally
        {
            using var baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);
            baseKey.DeleteSubKeyTree(rootPath, false);
        }
    }

    public void Dispose() => temp.Dispose();
}