namespace JdkFind.Providers;

/// <summary>Scans the conventional Linux JVM directory <c>/usr/lib/jvm</c>.</summary>
public sealed class LinuxJvmProvider(string? commonPrefix) : JvmProviderBase
{
    public override string Name => "linux";

    public LinuxJvmProvider() : this(OperatingSystem.IsLinux() ? "/usr/lib/jvm" : null) { }

    public override IEnumerable<string> GetJavaHomes() =>
        commonPrefix != null ? ScanPrefixes([commonPrefix]) : Enumerable.Empty<string>();
}
