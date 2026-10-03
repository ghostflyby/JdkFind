namespace JdkFind.Providers;

/// <summary>Scans the conventional Linux JVM directory <c>/usr/lib/jvm</c>.</summary>
public sealed class LinuxJvmProvider(string? commonPrefix) : ICommonPrefixJvmProvider
{
    /// <inheritdoc />
    public string Name => "linux";

    /// <summary>Scans <c>/usr/lib/jvm</c> on Linux; nothing elsewhere.</summary>
    public LinuxJvmProvider() : this(OperatingSystem.IsLinux() ? "/usr/lib/jvm" : null) { }

    /// <inheritdoc />
    public string? CommonPrefix => commonPrefix;
}
