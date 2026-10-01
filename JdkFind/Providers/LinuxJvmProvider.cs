namespace JdkFind.Providers;

/// <summary>Scans the conventional Linux JVM directory <c>/usr/lib/jvm</c>.</summary>
public sealed class LinuxJvmProvider(string? commonPrefix) : JvmProviderBase
{
    public override string Name => "linux";

    public LinuxJvmProvider() : this(OperatingSystem.IsLinux() ? "/usr/lib/jvm" : null) { }

    public override IAsyncEnumerable<string> GetJavaHomesAsync(CancellationToken cancellationToken = default) =>
        commonPrefix != null ? ScanPrefixes([commonPrefix]).ToAsyncEnumerable() : AsyncEnumerable.Empty<string>();
}
