namespace JdkFind.Providers;

/// <summary>Scans the conventional Linux JVM directory <c>/usr/lib/jvm</c>.</summary>
public sealed class LinuxJvmProvider(string? commonPrefix) : ICommonPrefixJvmProvider
{
    public string Name => "linux";

    public LinuxJvmProvider() : this(OperatingSystem.IsLinux() ? "/usr/lib/jvm" : null) { }

    public string? CommonPrefix => commonPrefix;
}
