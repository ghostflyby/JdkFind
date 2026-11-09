namespace JdkFind.Providers;

/// <summary>
///     Scans the per-user Windows install location <c>%LOCALAPPDATA%\Programs</c>
///     where un-elevated MSI installers and unpacked distributions land (e.g. a
///     per-user Temurin under <c>Programs\Eclipse Adoptium\jdk-…</c>). The
///     prefixes deliberately overlap — <c>Programs</c> itself plus every vendor
///     directory below it — so homes sitting directly under <c>Programs</c> are
///     probed under the root while the vendor-then-version nesting is probed
///     under the vendor.
/// </summary>
public sealed class WindowsUserProgramsJvmProvider(string? programsDirectory) : ICommonPrefixesJvmProvider
{
    /// <inheritdoc />
    public string Name => "windows-user-programs";

    /// <summary>Scans the default per-user programs directory.</summary>
    public WindowsUserProgramsJvmProvider() : this(ResolveDefaultProgramsDirectory()) { }

    IEnumerable<string> ICommonPrefixesJvmProvider.GetCommonPrefixes()
    {
        if (programsDirectory is null)
            yield break;

        yield return programsDirectory;
        foreach (var vendor in JvmScanning.EnumerateGuarded(programsDirectory))
            yield return vendor;
    }

    private static string? ResolveDefaultProgramsDirectory()
    {
        if (!OperatingSystem.IsWindows())
            return null;

        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return string.IsNullOrEmpty(localAppData) ? null : Path.Combine(localAppData, "Programs");
    }
}
