namespace JdkFind.Providers;

/// <summary>Scans the system-wide macOS JVM directory <c>/Library/Java/JavaVirtualMachines</c>.</summary>
public sealed class MacOsSystemJvmProvider(string? commonPrefix) : JvmProviderBase
{
    public override string Name => "macos-system";

    public MacOsSystemJvmProvider() : this(OperatingSystem.IsMacOS() ? "/Library/Java/JavaVirtualMachines" : null) { }

    public override IAsyncEnumerable<string> GetJavaHomesAsync(CancellationToken cancellationToken = default) =>
        commonPrefix != null ? ScanPrefixes([commonPrefix]).ToAsyncEnumerable() : AsyncEnumerable.Empty<string>();
}
