namespace JdkFind.Providers;

/// <summary>
///     Scans MacPorts JVM installs under <c>/opt/local/share/java</c> on macOS —
///     the ports install the image under the prefix rather than into
///     <c>/Library/Java</c>, so <c>/usr/libexec/java_home</c> cannot see them
///     (and neither can the macOS provider above).
/// </summary>
public sealed class MacPortsJvmProvider(string? commonPrefix) : ICommonPrefixJvmProvider
{
    /// <inheritdoc />
    public string Name => "macports";

    /// <summary>Scans the default MacPorts prefix.</summary>
    public MacPortsJvmProvider() : this(ResolveDefaultPrefix()) { }

    /// <inheritdoc />
    public string? CommonPrefix => commonPrefix;

    private static string? ResolveDefaultPrefix() =>
        OperatingSystem.IsMacOS() ? "/opt/local/share/java" : null;
}
