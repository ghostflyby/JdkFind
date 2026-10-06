namespace JdkFind;

/// <summary>
///     A java binary that can run programs — the only thing running a java
///     program actually needs. The probed properties are the binary's own word;
///     <c>-XshowSettings:properties</c> output is an implementation detail rather
///     than a spec promise, so any property except <c>java.version</c> may be
///     absent (null). Where a release file backs the binary, its values fill the
///     properties the binary did not report.
/// </summary>
public sealed record JavaExecutable
{
    /// <summary>The binary's path, taken verbatim.</summary>
    public required string Path { get; init; }

    /// <summary>
    ///     Why the binary could not be started — a spawn error, the probe
    ///     timeout, or a non-zero exit carrying the output tail — or null when it
    ///     ran, reported nothing parseable, or no probe ran.
    /// </summary>
    public string? StartFailure { get; init; }

    /// <summary>The binary's version: the probed <c>java.version</c>, falling back
    /// to the release file's <c>JAVA_VERSION</c> where a release file backs the
    /// binary.</summary>
    public required JvmVersion Version { get; init; }

    /// <summary>The probed <c>java.vendor</c>, falling back to the release file's
    /// <c>IMPLEMENTOR</c>; null when neither reports it.</summary>
    public string? VendorRaw { get; init; }

    /// <summary>The normalized vendor that <see cref="VendorRaw" /> matched, or
    /// <see cref="JvmVendor.Unknown" />.</summary>
    public JvmVendor Vendor { get; init; }

    /// <summary>The probed <c>os.name</c>, falling back to the release file's
    /// <c>OS_NAME</c>; null when neither reports it.</summary>
    public string? OsName { get; init; }

    /// <summary>The probed <c>os.arch</c>, falling back to the release file's
    /// <c>OS_ARCH</c>; null when neither reports it.</summary>
    public string? Architecture { get; init; }

    /// <summary>The probed <c>java.runtime.name</c>; null when not probed or not reported.</summary>
    public string? RuntimeName { get; init; }

    /// <summary>The probed <c>java.runtime.version</c>; null when not probed or not reported.</summary>
    public string? RuntimeVersion { get; init; }

    /// <summary>The probed <c>java.vm.name</c>; null when not probed or not reported.</summary>
    public string? VmName { get; init; }

    /// <summary>The probed <c>java.vm.version</c>; null when not probed or not reported.</summary>
    public string? VmVersion { get; init; }

    /// <summary>Summarizes the binary: version, vendor and path.</summary>
    public override string ToString() => $"{Version} {Vendor} — {Path}";
}
