namespace JdkFind.Providers;

/// <summary>Scans the system-wide macOS JVM directory <c>/Library/Java/JavaVirtualMachines</c>.</summary>
public sealed class MacOsSystemJvmProvider(string? commonPrefix) : JvmProviderBase
{
    public override string Name => "macos-system";

    public MacOsSystemJvmProvider() : this(OperatingSystem.IsMacOS() ? "/Library/Java/JavaVirtualMachines" : null) { }

    public override IEnumerable<string> GetJavaHomes() =>
        commonPrefix != null ? ScanPrefixes([commonPrefix]) : Enumerable.Empty<string>();
}
