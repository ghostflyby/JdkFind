namespace JdkFind.Providers;

/// <summary>
///     Scans the per-user Windows install location <c>%LOCALAPPDATA%\Programs</c>
///     where un-elevated MSI installers and unpacked distributions land (e.g. a
///     per-user Temurin under <c>Programs\Eclipse Adoptium\jdk-…</c>). Each
///     directory below is probed itself and then one level deep, covering both a
///     home sitting directly under <c>Programs</c> and the vendor-then-version
///     nesting the installers use.
/// </summary>
public sealed class WindowsUserProgramsJvmProvider(string? programsDirectory) : IJvmProvider
{
    /// <inheritdoc />
    public string Name => "windows-user-programs";

    /// <summary>Scans the default per-user programs directory.</summary>
    public WindowsUserProgramsJvmProvider() : this(ResolveDefaultProgramsDirectory()) { }

    IEnumerable<string> IJvmProvider.GetJavaHomes() =>
        JvmScanning.EnumerateGuarded(programsDirectory ?? string.Empty)
            .SelectMany(JvmScanning.ProbeNested);

    private static string? ResolveDefaultProgramsDirectory()
    {
        if (!OperatingSystem.IsWindows())
            return null;

        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return string.IsNullOrEmpty(localAppData) ? null : Path.Combine(localAppData, "Programs");
    }
}
